namespace SMSMais.Integrador.Agente;

/// <summary>Uma etapa (campo <c>etapa</c> do formulário) que é gatilho de negócio no SISREG.</summary>
/// <param name="Evento">Rótulo do evento (cancelamento, agendamento…).</param>
/// <param name="Escrita">A operação altera dado no SISREG.</param>
/// <param name="Comando">Modo mínimo: o comando enviado ao SMSMais ("agendou"/"cancelou").</param>
/// <param name="NumeroDe">Modo mínimo: onde está o nº da solicitação — "envio" ou "resposta".</param>
/// <param name="CampoNumero">Quando "envio", o campo do formulário que tem o número.</param>
public sealed record Etapa(string Evento, bool Escrita, string? Comando = null, string? NumeroDe = null, string? CampoNumero = null);

/// <summary>
/// Catálogo dos endpoints e etapas do SISREG já mapeados — porte fiel de
/// <c>SMSMais.chrome/endpoints.js</c>. Mudou lá, muda aqui.
/// </summary>
public static class Catalogo
{
    public static readonly IReadOnlyDictionary<string, string> Endpoints = new Dictionary<string, string>
    {
        ["/"] = "Login",
        ["/cgi-bin/index"] = "Início (moldura com menu)",
        ["/cgi-bin/avisos"] = "Avisos (tela inicial do f_main)",
        ["/cgi-bin/cadweb50"] = "Consulta CADSUS (CPF/CNS)",
        ["/cgi-bin/marcar"] = "Solicitação de Consultas Ambulatoriais",
        ["/cgi-bin/cons_verificar"] = "Consulta de Autorização/Cancelamento",
        ["/cgi-bin/gerenciador_solicitacao"] = "Consulta de Solicitações Ambulatoriais",
        ["/cgi-bin/cons_agendas"] = "Impressão/Confirmação de Agendas",
        ["/cgi-bin/cons_escalas"] = "Consulta de Escalas Ambulatoriais",
        ["/cgi-bin/cons_marcados_reg"] = "Agendados pela Regulação",
        ["/cgi-bin/expo_solicitacoes"] = "Arquivo Agendamento (TXT)",
        ["/cgi-bin/cons_unidade"] = "Unidades",
        ["/cgi-bin/config_preparo"] = "Cadastro de Preparo",
        ["/cgi-bin/sisreg_ajax"] = "AJAX (listas encadeadas)",
        ["/cgi-bin/recaptcha"] = "CAPTCHA anti-robô",
        ["/cgi-bin/autorizador"] = "Autorizador (fila do regulador)",
    };

    public static readonly IReadOnlyDictionary<string, Etapa> Etapas = new Dictionary<string, Etapa>
    {
        ["ACESSO"] = new("login", false),
        // A GRAVAÇÃO da marcação: o nº da solicitação só existe na tela de confirmação (resposta).
        ["MARCAR"] = new("agendamento", true, Comando: "agendou", NumeroDe: "resposta"),
        ["EXCLUIR_SOLICITACAO"] = new("cancelamento", true, Comando: "cancelou", NumeroDe: "envio", CampoNumero: "codigo_solicitacao"),
        ["CANCELAR_SOLICITACAO"] = new("cancelamento", true, Comando: "cancelou", NumeroDe: "envio", CampoNumero: "co_seq_solicitacao"),
        ["REENVIAR_REGULACAO"] = new("devolucao-regulacao", true),
        ["Confirma"] = new("confirmacao-comparecimento", true),
        ["Falta"] = new("falta", true),
        ["LST_VAGAS"] = new("marcacao-vagas", false),
        ["EXIBIR_FICHA"] = new("ficha-solicitacao", false),
        ["EXPORTAR_ESCALAS"] = new("export-escalas", false),
        ["INSERIR_PREPARO"] = new("preparo", true),
        ["ATUALIZAR_PREPARO"] = new("preparo", true),
        ["EXCLUIR_PREPARO"] = new("preparo", true),
    };

    /// <summary>Campos que NUNCA saem do navegador nem aparecem na janela.</summary>
    public static readonly HashSet<string> CamposSensiveis = ["senha", "senha_256"];
}
