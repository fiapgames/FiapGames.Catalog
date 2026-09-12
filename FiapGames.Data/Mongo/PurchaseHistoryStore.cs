using FiapGames.Core.Models;
using FiapGames.Core.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace FiapGames.Data.Mongo;

public sealed class PurchaseHistoryStore : IPurchaseHistoryStore
{
    public const string CollectionName = "purchase_events";

    private readonly IMongoCollection<PurchaseEvent> _collection;

    public PurchaseHistoryStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<PurchaseEvent>(CollectionName);
    }

    public async Task AppendAsync(PurchaseEvent purchaseEvent, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(purchaseEvent, cancellationToken: cancellationToken);
    }

    public async Task<List<PurchaseEvent>> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(e => e.OrderId == orderId)
            .SortBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    // Chamado explicitamente pelo Program.cs, ANTES de qualquer outro código tocar o
    // Mongo (inclusive antes de `GetCollection<PurchaseEvent>`, usado só para criar o
    // índice). Não dá pra confiar em rodar isso lazy num construtor estático: assim que
    // alguém pede o serializer de PurchaseEvent pela primeira vez — mesmo que só pra
    // criar o índice — o driver já monta e CACHEIA um class map default (com Guid no
    // formato global Unspecified, que quebra), e depois disso não tem como substituir.
    public static void RegisterSerializers()
    {
        // O driver serializa `decimal` como string por padrão; sem isso o Price
        // vira texto no Mongo em vez de Decimal128, quebrando agregações numéricas.
        BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
        BsonSerializer.RegisterSerializer(
            typeof(decimal?),
            new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

        // Guid NÃO pode ser registrado globalmente (BsonSerializer.RegisterSerializer):
        // o driver 3.x já registra o seu próprio GuidSerializer (Unspecified) assim que
        // toca o Mongo pela primeira vez, e uma segunda chamada global lança "There is
        // already a serializer registered for type Guid". Por isso o mapeamento fica
        // local a este class map, via BsonType.String — evita totalmente o assunto
        // GuidRepresentation (Standard/CSharpLegacy/etc.), que só existe por causa de
        // como cada driver serializava Guid como binário no passado.
        if (!BsonClassMap.IsClassMapRegistered(typeof(PurchaseEvent)))
        {
            BsonClassMap.RegisterClassMap<PurchaseEvent>(classMap =>
            {
                classMap.AutoMap();

                var guidAsString = new GuidSerializer(BsonType.String);
                classMap.GetMemberMap(nameof(PurchaseEvent.Id)).SetSerializer(guidAsString);
                classMap.GetMemberMap(nameof(PurchaseEvent.OrderId)).SetSerializer(guidAsString);
                classMap.GetMemberMap(nameof(PurchaseEvent.UserId)).SetSerializer(guidAsString);
                classMap.GetMemberMap(nameof(PurchaseEvent.GameId)).SetSerializer(guidAsString);
            });
        }
    }
}
