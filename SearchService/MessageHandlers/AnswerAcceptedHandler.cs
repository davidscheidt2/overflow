using System.Text.RegularExpressions;
using Contracts;
using Typesense;

namespace SearchService.MessageHandlers;

public class AnswerAcceptedHandler(ITypesenseClient client)
{
    public async Task HandleAsync(AnswerAccepted message)
    {
        await client.UpdateDocument("questions", message.QuestionId, new {
            HasAcceptedAnswer = true
        });
    }
}