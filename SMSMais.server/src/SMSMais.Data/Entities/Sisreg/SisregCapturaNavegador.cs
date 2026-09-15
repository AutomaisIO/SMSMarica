namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Uma operação observada no SISREG pela extensão de navegador (ADR pendente) — o ENVIO
/// (URL + campos do formulário), o RETORNO (HTML da tela / corpo de AJAX) ou o operador logado.
/// <para>
/// Tabela <b>só de inclusão</b>, matéria-prima da fase de análise: capturamos tudo para
/// entender as requisições reais do SISREG e, depois, desenhar os endpoints definitivos que
/// vão "antecipar" o que hoje só chega pela varredura diária. Não tem edição nem exclusão
/// lógica — quando a análise terminar, o acervo é descartado em bloco.
/// </para>
/// <para>
/// A extensão só OBSERVA o SISREG (nunca dispara requisição para lá): cada linha é uma ação que
/// o próprio operador fez, com a senha dele. A senha (<c>senha</c>/<c>senha_256</c>) é mascarada
/// na origem e nunca chega aqui.
/// </para>
/// </summary>
public class SisregCapturaNavegador
{
    public Guid Id { get; set; }

    /// <summary>Quando o servidor recebeu a captura.</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Quando a operação ocorreu no navegador (relógio do cliente).</summary>
    public DateTime? OcorridoEm { get; set; }

    /// <summary>Usuário do SMSMarica logado na extensão (dono da sessão que autorizou o envio).</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Id anônimo da instalação da extensão (um por perfil de Chrome) — separa os PCs do piloto.</summary>
    public string InstallId { get; set; } = string.Empty;

    /// <summary>Versão da extensão que enviou.</summary>
    public string? Versao { get; set; }

    /// <summary>Operador do SISREG lido da barra "Operador:" (quem estava logado no SISREG).</summary>
    public string? OperadorSisreg { get; set; }

    /// <summary>Tipo do evento: <c>requisicao</c> (envio), <c>resposta</c> (HTML da tela),
    /// <c>ajax</c> (corpo de fetch/XHR) ou <c>operador</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Método HTTP (quando aplicável).</summary>
    public string? Metodo { get; set; }

    /// <summary>Caminho no SISREG, ex.: <c>/cgi-bin/cons_agendas</c>.</summary>
    public string? Caminho { get; set; }

    /// <summary>A <c>etapa</c> do formulário do SISREG (ex.: <c>EXCLUIR_SOLICITACAO</c>, <c>Confirma</c>).</summary>
    public string? Etapa { get; set; }

    /// <summary>Evento de negócio derivado da etapa (cancelamento, confirmacao, falta…), quando conhecido.</summary>
    public string? Evento { get; set; }

    /// <summary>A operação altera dado no SISREG (derivado da etapa).</summary>
    public bool Escrita { get; set; }

    /// <summary>Status da requisição (código HTTP ou erro), quando conhecido.</summary>
    public string? Status { get; set; }

    /// <summary>O evento estruturado como veio da extensão (campos do formulário inclusos), em jsonb.</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Conteúdo grande fora do jsonb: HTML da tela ou corpo do AJAX. Pode ser nulo.</summary>
    public string? Conteudo { get; set; }
}
