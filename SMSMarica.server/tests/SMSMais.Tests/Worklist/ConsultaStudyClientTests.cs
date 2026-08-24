using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Worklist;

namespace SMSMais.Tests.Worklist;

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
    public async Task BuscarStudiesPorData_extrai_uid_accession_e_patientId()
    {
        const string json = """
        [
          { "0020000D": { "vr": "UI", "Value": ["2.25.111"] },
            "00080050": { "vr": "SH", "Value": ["260702062"] },
            "00100020": { "vr": "LO", "Value": ["96254947749"] } },
          { "0020000D": { "vr": "UI", "Value": ["2.25.222"] } },
          { "00080050": { "vr": "SH", "Value": ["SEM-UID"] } }
        ]
        """;
        var lista = await Cliente(new HandlerFixo(json))
            .BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), 100);

        lista.Should().HaveCount(2); // o item sem StudyInstanceUID é descartado
        lista[0].StudyInstanceUID.Should().Be("2.25.111");
        lista[0].AccessionNumber.Should().Be("260702062");
        lista[0].PatientId.Should().Be("96254947749");
        lista[1].StudyInstanceUID.Should().Be("2.25.222");
        lista[1].AccessionNumber.Should().BeNull();
        lista[1].PatientId.Should().BeNull();
    }

    [Fact]
    public async Task BuscarStudiesPorData_monta_range_campos_ordenacao_e_pagina_inicial()
    {
        var handler = new HandlerFixo("[]");
        await Cliente(handler).BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), 250);

        handler.UltimaUrl.Should().Contain("StudyDate=20260701-20260708");
        handler.UltimaUrl.Should().Contain("includefield=00080050");
        handler.UltimaUrl.Should().Contain("includefield=0020000D");
        handler.UltimaUrl.Should().Contain("includefield=00100020");
        // Ordenação determinística: sem orderby o paging por offset pula/duplica linhas.
        handler.UltimaUrl.Should().Contain("orderby=-StudyDate,-StudyTime");
        handler.UltimaUrl.Should().Contain("limit=100"); // paginado (cap QIDO do dcm4chee)
        handler.UltimaUrl.Should().Contain("offset=0");
    }

    [Fact]
    public async Task BuscarStudiesPorData_falha_do_pacs_lanca_em_vez_de_lista_vazia()
    {
        // PACS fora do ar não pode ser indistinguível de "janela vazia" — o resync
        // reportaria varredura limpa sem ter varrido nada.
        var acao = () => Cliente(new HandlerFixo("", HttpStatusCode.InternalServerError))
            .BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), 10);

        await acao.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class HandlerSequencia(params string[] respostas) : HttpMessageHandler
    {
        private int _i;
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.AbsoluteUri);
            var json = _i < respostas.Length ? respostas[_i++] : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/dicom+json"),
            });
        }
    }

    [Fact]
    public async Task BuscarStudiesPorData_pagina_com_offset_ate_pagina_curta()
    {
        static string Pagina(int inicio, int n) => "[" + string.Join(',', Enumerable.Range(inicio, n)
            .Select(i => $$"""{ "0020000D": { "vr": "UI", "Value": ["2.25.{{i}}"] } }""")) + "]";

        // 1ª página cheia (100) + 2ª curta (1) → para; sem 3ª requisição.
        var handler = new HandlerSequencia(Pagina(0, 100), Pagina(100, 1));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://pacs/") };
        var cliente = new ConsultaStudyClient(http, NullLogger<ConsultaStudyClient>.Instance);

        var lista = await cliente.BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), 500);

        lista.Should().HaveCount(101);
        handler.Urls.Should().HaveCount(2);
        handler.Urls[0].Should().Contain("limit=100").And.Contain("offset=0");
        handler.Urls[1].Should().Contain("limit=100").And.Contain("offset=100");
    }

    [Fact]
    public async Task BuscarStudiesPorData_corta_no_teto_apos_pagina_cheia()
    {
        static string Pagina(int inicio, int n) => "[" + string.Join(',', Enumerable.Range(inicio, n)
            .Select(i => $$"""{ "0020000D": { "vr": "UI", "Value": ["2.25.{{i}}"] } }""")) + "]";

        // Teto 150 no meio da 2ª página: a página é sempre cheia (offset estável) e o
        // excedente é cortado no retorno.
        var handler = new HandlerSequencia(Pagina(0, 100), Pagina(100, 100));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://pacs/") };
        var cliente = new ConsultaStudyClient(http, NullLogger<ConsultaStudyClient>.Instance);

        var lista = await cliente.BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), 150);

        lista.Should().HaveCount(150);
        handler.Urls.Should().HaveCount(2);
        handler.Urls[1].Should().Contain("limit=100"); // página fixa, nunca parcial
    }

    [Fact]
    public async Task BuscarStudiesPorData_mesma_data_nao_gera_range()
    {
        var handler = new HandlerFixo("[]");
        await Cliente(handler).BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 7), new DateOnly(2026, 7, 7), 10);

        handler.UltimaUrl.Should().Contain("StudyDate=20260707&");
    }

    [Fact]
    public async Task BuscarStudiesPorData_204_retorna_vazio()
    {
        (await Cliente(new HandlerFixo("", HttpStatusCode.NoContent))
            .BuscarStudiesPorDataAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), 10))
            .Should().BeEmpty();
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
