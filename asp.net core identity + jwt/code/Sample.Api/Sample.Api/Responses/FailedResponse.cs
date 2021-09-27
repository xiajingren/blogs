using System.Collections.Generic;

namespace Sample.Api.Responses
{
    public class FailedResponse
    {
        public IEnumerable<string> Errors { get; set; }
    }
}