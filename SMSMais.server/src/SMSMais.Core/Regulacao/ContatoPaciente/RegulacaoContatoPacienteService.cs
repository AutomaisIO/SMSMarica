using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Enriquecimento;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.ContatoPaciente;

/// <summary>O agendamento como o sistema de destino o mostra.</summary>
/// <param name="DataHora">Instante (UTC) — só o SISREG entrega data e hora como dado.</param>
/// <param name="DataTexto">Como o SER, o SERNIT e o ESUS escrevem a data (texto da tela deles).</param>
public sealed record AgendamentoNoDestinoDto(
    SistemaRegulacao Sistema,
    string? Situacao,
    DateTime? DataHora,
    string? DataTexto,
    string? Local);

/// <summary>
/// A mensagem é um aviso automático (confirmação, lembrete...) — e de qual pedido. O paciente pode
/// ter vários pedidos: sem isto, o "Sua consulta foi agendada!" de outro pedido passaria por aviso
/// deste.
/// </summary>
/// <param name="DestePedido">O aviso é do agendamento desta solicitação.</param>
/// <param name="Procedimento">O procedimento do pedido do aviso (o texto da mensagem não diz).</param>
public sealed record AvisoDaMensagemDto(
    string Finalidade,
    bool DestePedido,
    string? Procedimento,
    DateTime? DataAgendada);

/// <summary>Uma mensagem de WhatsApp trocada com o paciente.</summary>
/// <param name="DoPaciente">Chegou do paciente (resposta). Falso = saiu da plataforma.</param>
/// <param name="Status">Enviada, Entregue, Lida, Falha — ou Recebida, quando é do paciente.</param>
/// <param name="Autor">Quem da equipe escreveu. Nulo em mensagem automática e nas do paciente.</param>
/// <param name="Tipo">Texto, Template, Imagem, Documento, Audio, Video, Robo.</param>
/// <param name="Aviso">Preenchido quando a mensagem é um aviso automático da plataforma.</param>
public sealed record MensagemAoPacienteDto(
    Guid Id,
    DateTime Em,
    bool DoPaciente,
    string Status,
    string? Telefone,
    string? Autor,
    string? Tipo,
    string? Modelo,
    string? Texto,
    string? Erro,
    AvisoDaMensagemDto? Aviso);

/// <summary>
/// O que a plataforma sabe do contato com o paciente sobre esta solicitação: o aviso automático do
/// agendamento (com os recibos e a resposta) e as mensagens de WhatsApp trocadas desde que ela foi
/// aberta.
/// </summary>
/// <param name="DestinoComAvisoAutomatico">A plataforma avisa sozinha os agendamentos deste destino
/// (hoje, só o SISREG). Falso = do SER/SERNIT/ESUS nada sai sem alguém mandar.</param>
/// <param name="Confirmacao">Pendente, Confirmada ou Cancelada — a resposta ao aviso do SISREG.
/// Nulo quando não houve aviso nem confirmação por outro canal.</param>
public sealed record ContatoPacienteRegulacaoDto(
    AgendamentoNoDestinoDto? Agendamento,
    bool DestinoComAvisoAutomatico,
    string? Confirmacao,
    DateTime? ConfirmadoEm,
    string? ConfirmadoCanal,
    string? MotivoCancelamentoPaciente,
    IReadOnlyList<HistoricoComunicacaoDto> Avisos,
    IReadOnlyList<HistoricoContatoDto> Contatos,
    IReadOnlyList<MensagemAoPacienteDto> Mensagens,
    string? TelefoneConfirmado);

public interface IRegulacaoContatoPacienteService
{
    /// <summary>
    /// O contato com o paciente sobre a solicitação. Passa pelo mesmo escopo do detalhe — é dado
    /// de paciente, e quem não enxerga a solicitação não o enxerga por esta porta.
    /// </summary>
    Task<ContatoPacienteRegulacaoDto> ObterAsync(Guid solicitacaoId, CancellationToken ct);
}

/// <summary>
/// O contato com o paciente visto da solicitação da regulação — para a unidade e o regulador
/// saberem se o paciente foi avisado, se leu e o que respondeu, sem abrir o módulo Conversas.
///
/// <para><b>Só lê.</b> Nada aqui envia mensagem: o aviso automático é do SISREG
/// (<c>ComunicacaoPaciente</c>, ancorada na espinha <c>solicitacao</c>), e o SER, o SERNIT e o
/// ESUS ainda não têm aviso automático — por isso as mensagens da conversa entram junto: é
/// por elas que se vê quando alguém da equipe avisou à mão.</para>
///
/// <para><b>Mensagens:</b> as do paciente desde a abertura da solicitação, das duas direções.
/// Nota interna e mensagem de sistema do módulo Conversas ficam de fora — são da equipe, não do
/// contato com o paciente.</para>
/// </summary>
public sealed class RegulacaoContatoPacienteService(
    SmsMaisDbContext db,
    IRegulacaoEscopo escopo,
    ISolicitacaoHistoricoService historico,
    IPacientesService pacientes) : IRegulacaoContatoPacienteService
{
    /// <summary>Teto de mensagens na tela — a conversa inteira continua no módulo Conversas.</summary>
    internal const int MaxMensagens = 40;

    /// <summary>Corte do texto de cada mensagem — o modelo do aviso já passa de 600 caracteres.</summary>
    internal const int MaxTexto = 700;

    public async Task<ContatoPacienteRegulacaoDto> ObterAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", solicitacaoId);
        await escopo.ExigirAlcanceAsync(s, ct);

        var agendamento = await AgendamentoAsync(s.SistemaDestino, s.SolicitacaoId, s.SerSolicitacaoId,
            s.SernitSolicitacaoId, s.EsusSgSolicitacaoId, ct);

        // Aviso automático e contatos registrados existem só na espinha (SISREG).
        IReadOnlyList<HistoricoComunicacaoDto> avisos = [];
        IReadOnlyList<HistoricoContatoDto> contatos = [];
        string? confirmacao = null;
        DateTime? confirmadoEm = null;
        string? canal = null;
        string? motivoCancelamento = null;
        if (s.SolicitacaoId is { } espinhaId)
        {
            var h = await historico.ObterAsync(espinhaId, ct);
            avisos = h.Comunicacoes;
            contatos = h.Contatos;

            var c = await db.Solicitacoes.AsNoTracking()
                .Where(x => x.Id == espinhaId)
                .Select(x => new { x.StatusConfirmacao, x.ConfirmadoEm, x.ConfirmadoCanal, x.MotivoCancelamentoPaciente })
                .FirstOrDefaultAsync(ct);
            // "Pendente" sem aviso nenhum é só o valor inicial da coluna — não é resposta de ninguém.
            if (c is not null && (avisos.Count > 0 || c.ConfirmadoCanal != null
                                  || c.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente))
            {
                confirmacao = c.StatusConfirmacao.ToString();
                confirmadoEm = c.ConfirmadoEm;
                canal = c.ConfirmadoCanal;
                motivoCancelamento = c.MotivoCancelamentoPaciente;
            }
        }

        var desde = s.CriadoEm;
        var linhas = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.PacienteId == s.PacienteId && m.OcorridoEm >= desde
                        && m.TipoMensagem != TipoMensagem.NotaInterna && m.TipoMensagem != TipoMensagem.Sistema)
            .OrderByDescending(m => m.OcorridoEm)
            .Take(MaxMensagens)
            .Select(m => new
            {
                m.Id, m.OcorridoEm, m.Direcao, m.Status, m.Telefone, m.AutorNomeExibicao, m.TipoMensagem,
                m.Template, m.Conteudo, m.MidiaLegenda, m.ErroMeta,
            })
            .ToListAsync(ct);

        // Quais destas mensagens são avisos automáticos, e de qual pedido.
        var ids = linhas.Select(m => m.Id).ToList();
        var avisosDasMensagens = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.MensagemWhatsAppId != null && ids.Contains(c.MensagemWhatsAppId.Value))
            .Select(c => new
            {
                MensagemId = c.MensagemWhatsAppId!.Value,
                c.SolicitacaoId,
                c.Finalidade,
                Procedimento = c.Solicitacao!.ProcedimentoTexto,
                c.Solicitacao.DataAgendada,
            })
            .ToListAsync(ct);
        var avisoPorMensagem = avisosDasMensagens
            .GroupBy(a => a.MensagemId)
            .ToDictionary(g => g.Key, g =>
            {
                var a = g.First();
                return new AvisoDaMensagemDto(
                    a.Finalidade.ToString(), a.SolicitacaoId == s.SolicitacaoId, a.Procedimento, a.DataAgendada);
            });

        var mensagens = linhas
            .Select(m => new MensagemAoPacienteDto(
                m.Id,
                m.OcorridoEm,
                m.Direcao == DirecaoMensagem.Entrada,
                m.Status.ToString(),
                m.Telefone,
                m.Direcao == DirecaoMensagem.Saida ? m.AutorNomeExibicao : null,
                m.TipoMensagem?.ToString(),
                m.Template,
                Cortar(m.Conteudo ?? m.MidiaLegenda),
                m.ErroMeta,
                avisoPorMensagem.GetValueOrDefault(m.Id)))
            .ToList();

        return new ContatoPacienteRegulacaoDto(
            agendamento,
            s.SistemaDestino == SistemaRegulacao.Sisreg,
            confirmacao, confirmadoEm, canal, motivoCancelamento,
            avisos, contatos, mensagens,
            await TelefoneConfirmadoAsync(s.PacienteId, ct));
    }

    private async Task<AgendamentoNoDestinoDto?> AgendamentoAsync(
        SistemaRegulacao? sistema, Guid? espinhaId, Guid? serId, Guid? sernitId, Guid? esusSgId,
        CancellationToken ct)
    {
        AgendamentoNoDestinoDto? a = null;
        if (sistema == SistemaRegulacao.Sisreg && espinhaId is { } id)
        {
            a = await db.Solicitacoes.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AgendamentoNoDestinoDto(
                    SistemaRegulacao.Sisreg,
                    x.Status.ToString(),
                    x.DataAgendada,
                    null,
                    db.Unidades.Where(u => u.Id == x.UnidadeExecutanteId).Select(u => u.Nome).FirstOrDefault()))
                .FirstOrDefaultAsync(ct);
        }
        else if (sistema == SistemaRegulacao.Ser && serId is { } ser)
        {
            a = await db.SerSolicitacoes.AsNoTracking()
                .Where(x => x.Id == ser)
                .Select(x => new AgendamentoNoDestinoDto(
                    SistemaRegulacao.Ser, x.Situacao.ToString(), null, x.AgendadoParaTexto, x.UnidadeExecutora))
                .FirstOrDefaultAsync(ct);
        }
        else if (sistema == SistemaRegulacao.Sernit && sernitId is { } sernit)
        {
            a = await db.SernitSolicitacoes.AsNoTracking()
                .Where(x => x.Id == sernit)
                .Select(x => new AgendamentoNoDestinoDto(
                    SistemaRegulacao.Sernit, x.Situacao.ToString(), null, x.AgendadoParaTexto, x.UnidadeExecutora))
                .FirstOrDefaultAsync(ct);
        }
        else if (sistema == SistemaRegulacao.EsusSg && esusSgId is { } esus)
        {
            var e = await db.EsusSgSolicitacoes.AsNoTracking()
                .Where(x => x.Id == esus)
                .Select(x => new { x.Situacao, x.DataHoraAgendadaTexto, x.DataAgendada, x.UnidadeExecutora, x.Local })
                .FirstOrDefaultAsync(ct);
            if (e is not null)
            {
                a = new AgendamentoNoDestinoDto(
                    SistemaRegulacao.EsusSg, e.Situacao.ToString(), null,
                    e.DataHoraAgendadaTexto ?? e.DataAgendada?.ToString("dd/MM/yyyy"),
                    e.UnidadeExecutora ?? e.Local);
            }
        }

        // Sem data o "agendamento" é só a situação — essa a tela já mostra no cabeçalho.
        return a is not null && (a.DataHora is not null || !string.IsNullOrWhiteSpace(a.DataTexto)) ? a : null;
    }

    /// <summary>
    /// O telefone que o paciente provou ser dele (OTP). Ficha que não carrega não derruba o quadro:
    /// o resto dele continua valendo.
    /// </summary>
    private async Task<string?> TelefoneConfirmadoAsync(Guid pacienteId, CancellationToken ct)
    {
        try
        {
            var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
            // Confirmado que depois foi negado ("não sou essa pessoa") deixa de valer.
            if (string.IsNullOrWhiteSpace(p.TelefoneVerificado)
                || NormaFicha.MesmoNumero(p.TelefoneVerificado, p.TelefoneNegado)) return null;
            return p.TelefoneVerificado;
        }
        catch (NaoEncontradoException)
        {
            return null;
        }
    }

    internal static string? Cortar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length <= MaxTexto ? t : t[..MaxTexto] + "…";
    }
}
