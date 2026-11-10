using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;

namespace OficinaMecanica.Seguranca.Functions.Tests.Support;

// Requisicao e resposta em memoria para chamar as Functions sem subir o host: o Functions isolated worker nao oferece um
// teste-duplo pronto, e o CreateResponse e o WriteStringAsync so precisam destes quatro membros.
internal sealed class FakeHttpRequestData : HttpRequestData
{
    public FakeHttpRequestData() : base(Mock.Of<FunctionContext>())
    {
    }

    public override Stream Body => Stream.Null;
    public override HttpHeadersCollection Headers { get; } = new();
    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
    public override Uri Url => new("http://localhost/health");
    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
    public override string Method => "GET";

    public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext);
}

internal sealed class FakeHttpResponseData : HttpResponseData
{
    public FakeHttpResponseData(FunctionContext context) : base(context)
    {
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; } = new();
    public override Stream Body { get; set; } = new MemoryStream();
    public override HttpCookies Cookies => Mock.Of<HttpCookies>();

    public string Texto()
    {
        Body.Position = 0;
        return new StreamReader(Body).ReadToEnd();
    }

    public string? ContentType => Headers.TryGetValues("Content-Type", out var valores) ? valores.FirstOrDefault() : null;
}
