# Validação da implementação OpenSearch

Data: 01/11/2026. Ambiente: Windows com Docker Desktop, containers Linux; OpenSearch 2.19.4, SQL Server 2022, .NET 10 e Kong 3.9.

## Resultado final

- Build/publish da imagem final do Catalog: passou.
- `dotnet test FiapGames.Catalog.sln -c Release --nologo`: **22 passaram, zero falhas, zero ignorados**, sem warnings NuGet na execução final.
- `node tests/search.integration.mjs`: **16 cenários passaram**, processo terminou com código 0.
- Oito arquivos YAML analisados sem erro; os dois arquivos Kong contêm `/search` com autenticação no serviço.
- Scripts Bash alterados validados com `bash -n`; Compose de Orchestration validado com `docker compose config --quiet`.
- `git diff --check` sem erros de whitespace.

## Cobertura com infraestrutura real e isolada

1. Startup da API e disponibilidade da busca através da configuração real do Kong.
2. JWT também exigido diretamente na API, sem passar pelo gateway.
3. JWT exigido pelo gateway.
4. Termo vazio, termo longo e paginação inválida rejeitados com 400.
5. `minecrfat` encontra `Minecraft`; título recebe maior relevância que descrição; total e preço corretos.
6. `acao` encontra `Ação Galáctica`.
7. Paginação estável sem repetir documentos entre as páginas verificadas.
8. Edição substitui título/preço no mesmo documento.
9. O título anterior deixa de encontrar o jogo editado.
10. Inativação exclui o jogo dos resultados ativos.
11. OpenSearch rejeita escrita com versão antiga (409).
12. Motor indisponível produz 503; readiness fica indisponível e liveness permanece disponível.
13. API reinicia com o OpenSearch ainda parado.
14. Escritas feitas durante a queda são recuperadas na versão mais recente após o reinício.
15. Exclusão do índice é recuperada automaticamente a partir do SQL Server.
16. Reconstrução explícita é idempotente, sem duplicar o jogo.

O runner cria projeto Compose, portas, senhas e dados próprios. Todos os containers e volumes do projeto de teste foram removidos ao final. Os dados de desenvolvimento não foram usados para esses cenários.

## Ambiente de desenvolvimento

A imagem final foi aplicada ao Compose compartilhado. `/health/ready` pelo Kong em `http://localhost:8000` retornou 200. `/search` sem JWT retornou 401 e com um token local de validação retornou 200. O catálogo de desenvolvimento está vazio; os exemplos dos testes não foram copiados para ele.

Permanecem rodando Catalog, SQL Server do Catalog, MongoDB, Redis, RabbitMQ, OpenSearch, Kong e Postgres do Kong. O container de bootstrap do Kong terminou com sucesso. Users, Payments, Notifications e frontend não foram iniciados para essa validação.

O restore inicial apontava vulnerabilidades em `System.Security.Cryptography.Pkcs` 9.0.4. O Catalog fixa agora a versão corrigida **9.0.21**, mantendo a mesma versão principal da dependência transitiva. A dependência SQLite dos testes também usa bundle atualizado. A execução final de build/test não emitiu aqueles avisos.

## Limite da validação

A configuração para Amazon OpenSearch Service foi implementada, incluindo o endpoint obtido do CloudFormation e sua preservação entre deploys. **Não houve acesso nem implantação na conta AWS**, portanto conectividade VPC/security group, permissões e comportamento do domínio real ainda precisam ser confirmados no ambiente do grupo.

O domínio atual usa HTTPS e controle por rede, sem assinatura IAM. Se a política real diferir do template versionado, adaptar o cliente antes da implantação. A implementação está concentrada nos repositórios Catalog e Orchestration; a validação descrita aqui não inclui publicação na AWS.

## Compilação geral dos repositórios

Após a implementação, todas as soluções .NET foram compiladas em Release, dentro de um container SDK .NET 10, a partir de cópias dos checkouts. O frontend foi instalado com `npm ci` e compilado com `npm run build`. Nenhum build terminou com erro.

| Repositório | Resultado |
|---|---|
| FiapGames.Contracts | Build e geração do pacote passaram; nenhum pacote foi publicado. |
| FiapGames.Catalog | Build passou; 22 testes passaram. |
| Fiap.Games.Users | Build passou; 11 testes passaram. |
| FiapGames.Payments | Build passou; 2 testes passaram. |
| FiapGames.Notifications | Build passou. |
| FiapGames.Web | TypeScript e build de produção Vite passaram. |
| FiapGames.Orchestration | Compose válido e sintaxe dos sete scripts Bash verificada; não possui solução compilável. |

Permanecem avisos preexistentes fora da implementação de busca: `System.Security.Cryptography.Pkcs` 9.0.4 vulnerável em Users, APIs obsoletas do SDK AWS, referência redundante de pacote e conflito de versões do EF Core nos testes de Payments. O `npm ci` reportou oito vulnerabilidades nas dependências existentes do frontend (uma moderada e sete altas), sem impedir o build. Essas dependências dos outros repositórios não foram alteradas.

Branches locais criadas para revisão e commit:

- Catalog: `feature/opensearch-search`.
- Orchestration: `feature/opensearch-infrastructure`.

Na conclusão desta validação, os outros cinco repositórios estavam na branch `main`, sem alterações em arquivos versionados.
