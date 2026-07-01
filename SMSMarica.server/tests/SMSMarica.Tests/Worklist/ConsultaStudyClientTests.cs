using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Worklist;

namespace SMSMarica.Tests.Worklist;

public class ConsultaStudyClientTests
{
    private sealed class HandlerFixo(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? UltimaUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaUrl = request.RequestUri?.AbsoluteUri;
            var resp = new HttpResponseMessage(status);
            if (status == HttpStatusCode.OK)
                resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/dicom+json");
            return Task.FromResult(resp);
        }
    }

    private static ConsultaStudyClient Cliente(HandlerFixo handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://pacs/") };
        return new ConsultaStudyClient(http, NullLogger<ConsultaStudyClient>.Instance);
    }

    [Fact]
    public async Task BuscarPorPatientId_extrai_studyUid_e_accession()
    {
        const string json = """
        [
          { "0020000D": { "vr": "UI", "Value": ["2.25.111"] }, "00080050": { "vr": "SH", "Value": ["SMS260625001"] } },
          { "0020000D": { "vr": "UI", "Value": ["2.25.222"] } }
        ]
        """;
        var lista = await Cliente(new HandlerFixo(json)).BuscarPorPatientIdAsync("SMS260625001");

        lista.Should().HaveCount(2);
        lista[0].StudyInstanceUID.Should().Be("2.25.111");
        lista[0].AccessionNumber.Should().Be("SMS260625001");
        lista[1].StudyInstanceUID.Should().Be("2.25.222");
        lista[1].AccessionNumber.Should().BeNull();
    }

    [Fact]
    public async Task BuscarPorPatientId_ignora_estudo_sem_studyUid()
    {
        const string json = """[ { "00080050": { "vr": "SH", "Value": ["X"] } } ]""";
        (await Cliente(new HandlerFixo(json)).BuscarPorPatientIdAsync("SMS1")).Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarPorPatientId_204_retorna_vazio()
    {
        (await Cliente(new HandlerFixo("", HttpStatusCode.NoContent)).BuscarPorPatientIdAsync("SMS1"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task StudyExistePorStudyUid_true_quando_ha_match()
    {
        const string json = """[ { "0020000D": { "vr": "UI", "Value": ["2.25.999"] } } ]""";
        (await Cliente(new HandlerFixo(json)).StudyExistePorStudyUidAsync("2.25.999")).Should().BeTrue();
    }

    [Fact]
    public async Task StudyExistePorStudyUid_false_quando_vazio()
    {
        (await Cliente(new HandlerFixo("[]")).StudyExistePorStudyUidAsync("2.25.0")).Should().BeFalse();
    }

    [Fact]
    public async Task BuscarPorPatientId_escapa_patientId_e_pede_campos()
    {
        var handler = new HandlerFixo("[]");
        await Cliente(handler).BuscarPorPatientIdAsync("SMS 1");

        handler.UltimaUrl.Should().Contain("PatientID=SMS%201");
        handler.UltimaUrl.Should().Contain("includefield=0020000D");
        handler.UltimaUrl.Should().Contain("includefield=00080050");
    }

    [Fact]
    public async Task ObterNomePaciente_VR_PN_objeto_Alphabetic_limpa_circunflexo()
    {
        const string json = """
        [ { "00100010": { "vr": "PN", "Value": [ { "Alphabetic": "SILVA^JOAO^MARIA" } ] } } ]
        """;
        (await Cliente(new HandlerFixo(json)).ObterNomePacienteAsync("2.25.1"))
            .Should().Be("SILVA JOAO MARIA");
    }

    [Fact]
    public async Task ObterNomePaciente_aceita_string_simples()
    {
        const string json = """[ { "00100010": { "vr": "PN", "Value": ["SILVA^JOAO"] } } ]""";
        (await Cliente(new HandlerFixo(json)).ObterNomePacienteAsync("2.25.1"))
            .Should().Be("SILVA JOAO");
    }

    [Fact]
    public async Task ObterNomePaciente_tag_ausente_retorna_null()
    {
        const string json = """[ { "0020000D": { "vr": "UI", "Value": ["2.25.1"] } } ]""";
        (await Cliente(new HandlerFixo(json)).ObterNomePacienteAsync("2.25.1")).Should().BeNull();
    }

    [Fact]
    public async Task ObterNomePaciente_estudo_inexistente_retorna_null()
    {
        (await Cliente(new HandlerFixo("[]")).ObterNomePacienteAsync("2.25.0")).Should().BeNull();
    }

    [Fact]
    public async Task ObterNomePaciente_204_retorna_null()
    {
        (await Cliente(new HandlerFixo("", HttpStatusCode.NoContent)).ObterNomePacienteAsync("2.25.1"))
            .Should().BeNull();
    }

    [Fact]
    public async Task ObterNomePaciente_pede_PatientName_e_escapa_studyUid()
    {
        var handler = new HandlerFixo("[]");
        await Cliente(handler).ObterNomePacienteAsync("2.25 9");

        handler.UltimaUrl.Should().Contain("StudyInstanceUID=2.25%209");
        handler.UltimaUrl.Should().Contain("includefield=00100010");
    }
}
