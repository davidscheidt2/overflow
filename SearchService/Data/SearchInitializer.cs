using Typesense;

namespace SearchService.Data;

public static class SearchInitializer
{
    public static async Task EnsureIndexExists(ITypesenseClient client)
    {
        const string schemeName = "questions";
        try
        {
            await client.RetrieveCollection(schemeName);
            Console.WriteLine($"Collection {schemeName} has been been created already.");
            return;
        }   
        catch (TypesenseApiNotFoundException)
        {
            Console.WriteLine($"Collection {schemeName} has not been created yet.");
        }

        var schema = new Schema(schemeName, new List<Field>
        {
            new Field("id", FieldType.String),
            new Field("title", FieldType.String),
            new Field("content", FieldType.String),
            new Field("tag", FieldType.StringArray),
            new Field("createdAt", FieldType.Int64),
            new Field("answerCount", FieldType.Int32),
            new Field("hasAcceptedAnswer", FieldType.Bool)
        })
        {
            DefaultSortingField = "createdAt"
        };
        await client.CreateCollection(schema);
        Console.WriteLine($"Collection {schemeName} has been created.");
    }
}