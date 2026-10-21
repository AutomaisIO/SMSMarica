using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Core.Fontes;

/// <summary>
/// Os sistemas com login de onde a plataforma lê dado (ADR-0076). Diferente das bases, são FIXOS:
/// existem na tela mesmo antes de configurados, porque o código que os lê já existe. Nesta entrega o
/// login continua em <c>integracao_credencial</c> (chave = provedor) e o da API-SISREG em
/// <c>sisreg_configuracao</c>.
/// </summary>
/// <param name="ChavesConexao">Chaves do JSON da credencial que são CONEXÃO — as únicas que a tela
///   de Fontes de dados lê e grava. As demais chaves do mesmo JSON são o uso dos motores (agenda,
///   varredura, estatística) e pertencem às telas de cada recurso.</param>
/// <param name="MotivoSemTeste">Nulo = tem botão de teste. Preenchido = por que não tem.</param>
public sealed record SistemaFonte(
    string Chave,
    string Nome,
    string Tipo,
    string? UrlPadrao,
    IReadOnlyList<string> ChavesConexao,
    bool LoginJunto,
    string? MotivoSemTeste,
    IReadOnlyList<string> UsadaPor);

public static class SistemasFonte
{
    /// <summary>Chave da API-SISREG (DATASUS/Elasticsearch), que mora em <c>sisreg_configuracao</c>.</summary>
    public const string SisregApi = "sisreg-api";

    private const string SessaoUnica =
        "Sessão única por operador: testar entraria com o mesmo login do robô e derrubaria a sessão dele.";

    public static readonly IReadOnlyList<SistemaFonte> Todos =
    [
        new(SisregWebSessao.Provedor, "SISREG III — login do sistema", "SisregWeb", SisregWebSessao.BaseUrlPadrao,
            ["baseUrl", "autoLogin"], LoginJunto: false, SessaoUnica,
            ["Sincronismo das unidades e escalas", "Fila e cancelamentos", "Cadastro do paciente (CADSUS)",
             "Indicadores de regulação", "Estatística de operadores"]),
        new(SisregApi, "SISREG — API (DATASUS)", "SisregApi", null,
            ["tipoAutenticacao"], LoginJunto: false, MotivoSemTeste: null,
            ["Consultas à API do SISREG (fila e solicitações)"]),
        new(SerWebSessao.Provedor, "SER — Sistema Estadual de Regulação", "SerWeb", SerWebSessao.BaseUrlPadrao,
            ["baseUrl"], LoginJunto: true, MotivoSemTeste: null,
            ["Espelho do SER (varredura)", "Envio de solicitações ao SER", "Cadastro do paciente",
             "Estatística de operadores"]),
        new(SernitWebSessao.Provedor, "SERNIT — SER de Niterói", "SernitWeb", SernitWebSessao.BaseUrlPadrao,
            ["baseUrl"], LoginJunto: true, MotivoSemTeste: null,
            ["Espelho do SERNIT (varredura)", "Envio de solicitações ao SERNIT", "Estatística de operadores"]),
        new(EsusSgSessao.Provedor, "ESUS — São Gonçalo", "EsusSgWeb", EsusSgSessao.ApiUrlPadrao,
            ["cliente", "apiUrl", "legadoUrl"], LoginJunto: true, MotivoSemTeste: null,
            ["Espelho do ESUS SG (varredura)", "Estatística de operadores"]),
        // A janela da madrugada é USO, mas a rotina noturna não tem tela própria: fica aqui até ter.
        new(CorrecaoTelefoneEsusService.Provedor, "e-SUS APS PEC (cadastro do cidadão)", "EsusPecWeb",
            EsusPecCliente.BaseUrlPadrao,
            ["baseUrl", "acessoId", "janelaInicio", "janelaFim"], LoginJunto: false,
            "Login cedido por uma servidora e sessão única: testar fora da madrugada derrubaria a sessão dela.",
            ["Correção de telefone (rotina da madrugada)", "Enriquecer a ficha (botão e-SUS)"]),
    ];

    public static SistemaFonte? Obter(string chave) =>
        Todos.FirstOrDefault(s => string.Equals(s.Chave, chave, StringComparison.OrdinalIgnoreCase));
}
