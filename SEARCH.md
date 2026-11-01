# Busca de jogos com Amazon OpenSearch

## Contrato

```http
GET /search?q=minecrfat&page=1&pageSize=20
Authorization: Bearer <token do UsersAPI>
```

```json
{
  "items": [
    {
      "game": {
        "id": "f1a8e281-d498-4c12-8886-21c040cbad53",
        "title": "Minecraft",
        "description": "Sandbox adventure",
        "price": 99.90,
        "genre": "Adventure",
        "active": true,
        "createdAt": "2026-11-01T12:00:00Z"
      },
      "score": 2.14
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

- `q`: obrigatório, até 200 caracteres; espaços em branco são rejeitados.
- `page`: a partir de 1; `pageSize`: de 1 a 100, padrão 20.
- Paginação limitada aos primeiros 10000 resultados, sem overflow de inteiros.
- `multi_match` com `fuzziness: AUTO`. Erros de inserção, exclusão, substituição e transposição são tolerados conforme o tamanho do termo.
- Título recebe peso 3, gênero peso 2 e descrição peso 1. A ordenação é por `_score` decrescente, com Id como desempate estável.
- Análise com tokenizer padrão, minúsculas e remoção de acentos: `acao` encontra `Ação`. Não usa stemming de português que poderia alterar títulos em inglês.
- Retorna apenas jogos ativos. A listagem tradicional `/games` mantém o comportamento anterior.
- `400`: parâmetros inválidos; `401`: JWT ausente/inválido; `503` com `Retry-After: 5`: motor indisponível ou índice em preparação. Não há fallback silencioso para pesquisa SQL.

## Escrita, recuperação e consistência

SQL Server continua sendo a fonte de verdade. A migration `AddSearchOutbox` adiciona `Games.SearchVersion` e `SearchIndexOutbox`, sem remover dados existentes. Jogos anteriores recebem versão 1.

Criação, edição e inativação incrementam a versão e gravam a pendência **na mesma transação** que o jogo. Após o commit, a API tenta atualizar o OpenSearch e espera o refresh antes de responder. Se o motor estiver indisponível, a escrita SQL é preservada, o evento fica na outbox e um warning registra a pendência. Nesse caso, uma resposta de sucesso do CRUD confirma a gravação no SQL; a pesquisa passa a refletir o dado após a recuperação.

O worker consulta as pendências a cada 5 segundos, lê a versão atual do jogo e reenvia. Apenas pendências com versão menor ou igual à indexada são removidas. Reiniciar a API não perde eventos. A invalidação de cache acontece depois dessa tentativa de sincronização, de modo que uma falha no Redis não impede o registro da pendência.

O `_id` do documento é o Id do jogo. `version_type=external_gte` permite retentativas idempotentes e rejeita documentos mais antigos. O token de concorrência no EF impede duas edições simultâneas de gerarem a mesma versão; a API retorna 409 para a escrita concorrente que perdeu a disputa. O número de versão é interno e não altera o DTO público do jogo.

O nome configurado (`games-v1`) é um alias para `games-v1-000001`. Escritas usam `require_alias`, evitando que uma requisição de CRUD recrie automaticamente um índice sem mapeamento durante a recuperação. Inativação atualiza `active=false`, em vez de excluir fisicamente o documento, preservando a versão e impedindo ressurreição por mensagens antigas.

`/health` verifica liveness sem depender da busca. A readiness do Kubernetes usa `/health/ready`, que aguarda a preparação do índice e retorna 503 durante indisponibilidade detectada. Isso impede que um novo Pod receba tráfego de busca antes da carga inicial.

No startup e quando o índice precisa ser recriado, o worker carrega os jogos do SQL em lotes de 200 e usa Bulk API. A busca fica indisponível enquanto a carga inicial daquela instância não termina. Uma queda temporária com o índice preservado exige apenas o reenvio da outbox. Se a aplicação for escalada, escritas concorrentes continuam protegidas pelas versões; não existe uma transação distribuída entre SQL e OpenSearch.

Não excluir jogos fisicamente por SQL fora da aplicação: o fluxo implementado de exclusão é a inativação. Não modificar o mapeamento de um índice em uso; mudanças futuras de schema devem usar novo índice/alias e um procedimento de migração.

## Configuração

| Variável | Padrão / significado |
|---|---|
| `OpenSearch__Url` | `http://localhost:9200` para execução no host; sobrescrita por ambiente. |
| `OpenSearch__IndexName` | `games-v1`, alias em minúsculas. |
| `OpenSearch__NumberOfReplicas` | `0`, coerente com o domínio atual de nó único. Aumentar com a topologia apropriada. |
| `OpenSearch__RetryIntervalSeconds` | `5`, entre 1 e 300. |
| `OpenSearch__RebuildOnly` | `false`; `true` executa a carga completa e encerra o processo. |

O cliente oficial `OpenSearch.Client` 2.3.0 é singleton. Timeout de requisição de 3 segundos; retentativa das escritas fica sob responsabilidade da outbox. Não há senha ou chave AWS no código.

### Docker Compose

Os Composes independente e compartilhado configuram `http://opensearch:9200`. Use somente um deles, pois compartilham portas e nomes de containers.

Em `FiapGames.Orchestration`, após gerar `.env` com o script existente:

```powershell
docker compose up -d --build catalog-api opensearch
# Entrada oficial pela porta 8000, quando o Kong estiver ativo:
docker compose up -d kong
```

Para obter um token pelo login normal, UsersAPI e seu banco também precisam estar disponíveis. Payments, Notifications, Konga e frontend não são necessários para consultar jogos.

O Kong no Compose usa TTL DNS de 5 segundos: o Docker pode trocar o IP da API após um restart, e o gateway precisa atualizar o upstream sem manter o endereço anterior por vários minutos.

### Kind e EKS

O Deployment do Catalog depende de `catalog-search-config`, gerenciado pela orquestração:

- Kind: `aws-local/up.sh` aplica `aws-local/catalog-search-config.yaml` com o DNS local do serviço.
- AWS: `infra/aws.sh` lê `DomainEndpoint` da stack OpenSearch e cria o ConfigMap com `https://<endpoint>` antes de implantar os serviços.
- A pipeline do Catalog reaplica os manifestos da aplicação, mas preserva esse ConfigMap. Assim, um deploy não substitui o endpoint AWS pelo endereço do Kind.
- Em um cluster já existente, preparar esse ConfigMap antes de implantar a nova imagem. Atualizar os arquivos Kong nos dois ambientes e reaplicar o bootstrap conforme o procedimento de Orchestration.

O template AWS atual usa TLS, criptografia e controle por VPC/security group, com política de acesso que aceita requisições não assinadas provenientes da rede permitida. Não exige Basic Auth ou SigV4. Se a política mudar para IAM ou controle granular, o cliente precisa ser adaptado antes do deploy. O notebook não acessa esse domínio privado diretamente sem conexão à VPC.

### Reconstrução manual

Com as dependências disponíveis e no diretório do Compose escolhido:

```powershell
docker compose run --rm -e OpenSearch__RebuildOnly=true catalog-api
```

O comando cria o índice se necessário, faz upsert dos jogos, processa pendências e encerra com erro se a operação falhar. Ele não inicia outro consumidor RabbitMQ nem expõe uma rota administrativa pública. Deve usar a mesma imagem e configuração da API. É seguro repetir a carga; documentos não são duplicados. A carga não apaga registros removidos manualmente do SQL, pois a aplicação usa inativação.

## Testes

Na raiz de `FiapGames.Catalog`:

```powershell
dotnet test FiapGames.Catalog.sln -c Release
node tests/search.integration.mjs
```

O primeiro comando precisa do SDK .NET 10. Os testes de sincronização usam SQLite em memória com transações reais, sem banco externo. Cobrem criação/edição/inativação, persistência da outbox durante indisponibilidade, recuperação após novo contexto, rollback SQL, disputa entre edições e rebuild em mais de um lote. Os validators verificam termos e limites de paginação.

Sem SDK no Windows, também é possível executar os testes pelo Docker, em PowerShell na raiz do repositório:

```powershell
docker build --target build -t fiapgames-catalog-sdk:local .
docker run --rm -v "${PWD}:/input:ro" -w /work fiapgames-catalog-sdk:local sh -c 'cp -a /input/. /work/ && dotnet test FiapGames.Catalog.sln -c Release --nologo'
```

O segundo comando precisa de Node.js 22+ e Docker Desktop com containers Linux. Requer `FiapGames.Orchestration` clonado ao lado, para usar a configuração real do Kong. Cria projeto Compose, portas e credenciais aleatórios. Sobe SQL Server, MongoDB, Redis, RabbitMQ, OpenSearch, Catalog e Kong isolados; remove seus próprios containers e volumes no final, inclusive em caso de falha. Não usa os dados de desenvolvimento.

O teste verifica pela API/gateway: JWT, validação, erro de digitação, relevância, acentos, preço, paginação, edição, inativação, proteção contra versão antiga, resposta 503, escrita durante indisponibilidade, reinício, reconstrução após exclusão do índice e carga explícita idempotente. Logs de sucesso identificam cada cenário; qualquer falha faz o processo sair com código diferente de zero.

Na AWS, repetir criação/edição e `/search?q=minecrfat` pelo gateway público e conferir o índice no domínio gerenciado. Os testes locais não comprovam permissões ou conectividade reais da conta AWS.

Resultado da validação executada: [tests/VALIDATION.md](tests/VALIDATION.md).
