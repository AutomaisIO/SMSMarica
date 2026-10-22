using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Notificacoes.AgendamentosExternos;
using SMSMais.Core.GuiasAgendamento;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Enriquecimento;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

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
/// <param name="DestinoComAvisoAutomatico">A plataforma avisa sozinha os agendamentos deste destino:
/// o SISREG sempre; o SER, o SERNIT e o ESUS desde 10/10/2026, enquanto a chave da Mensageria estiver
/// ligada. Falso = nada sai sem alguém mandar.</param>
/// <param name="Confirmacao">Pendente, Confirmada ou Cancelada — a resposta ao aviso. Nulo quando
/// não houve aviso nem confirmação por outro canal.</param>
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
    string? TelefoneConfirmado,
    GuiaDoAgendamentoDto? Guia = null);

public interface IRegulacaoContatoPacienteService
{
    /// <summary>
    /// O contato com o paciente sobre a solicitação. Passa pelo mesmo escopo do detalhe — é dado
    /// de paciente, e quem não enxerga a solicitação não o enxerga por esta porta.
    /// </summary>
    Task<ContatoPacienteRegulacaoDto> ObterAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>
    /// O mesmo quadro visto do pedido no espelho do SER/SERNIT/ESUS SG (a tela de detalhe de cada
    /// sistema) — é onde mora a maioria dos agendamentos externos, que não nasceram de uma
    /// solicitação da regulação. A permissão é a da tela do sistema (controller).
    /// </summary>
    Task<ContatoPacienteRegulacaoDto> ObterPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct);

    /// <summary>O PDF da guia do agendamento (ADR-0076), com o escopo do detalhe da solicitação.</summary>
    Task<ConteudoAcervo> ObterGuiaAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>O PDF da guia do agendamento do pedido no espelho. A permissão é a da tela do sistema.</summary>
    Task<ConteudoAcervo> ObterGuiaPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct);
}

/// <summary>
/// O contato com o paciente visto da solicitação da regulação — para a unidade e o regulador
/// saberem se o paciente foi avisado, se leu e o que respondeu, sem abrir o módulo Conversas.
///
/// <para><b>Só lê.</b> Nada aqui envia mensagem. O aviso automático do SISREG é ancorado na espinha
/// <c>solicitacao</c>; o do SER/SERNIT/ESUS SG (10/10/2026), no <c>agendamento_externo</c> do pedido do
/// espelho. As mensagens da conversa entram junto: é por elas que se vê quando alguém da equipe
/// avisou à mão.</para>
///
/// <para><b>Mensagens:</b> as do paciente desde a abertura da solicitação, das duas direções.
/// Nota interna e mensagem de sistema do módulo Conversas ficam de fora — são da equipe, não do
/// contato com o paciente.</para>
/// </summary>
public sealed class RegulacaoContatoPacienteService(
    SmsMaisDbContext db,
    IRegulacaoEscopo escopo,
    ISolicitacaoHistoricoService historico,
    IPacientesService pacientes,
    IConfirmacaoConfiguracaoService regras,
    IFilaGuiasService guias) : IRegulacaoContatoPacienteService
{
    /// <summary>Teto de mensagens na tela — a conversa inteira continua no módulo Conversas.</summary>
    internal const int MaxMensagens = 40;

    /// <summary>Corte do texto de cada mensagem — o modelo do aviso já passa de 600 caracteres.</summary>
    internal const int MaxTexto = 700;

    /// <summary>De onde o quadro é montado: a espinha (SISREG) ou a linha de um espelho.</summary>
    private sealed record Alvo(
        SistemaRegulacao? Sistema, Guid? EspinhaId, Guid? EspelhoId, Guid PacienteId, DateTime Desde);

    public async Task<ContatoPacienteRegulacaoDto> ObterAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", solicitacaoId);
        await escopo.ExigirAlcanceAsync(s, ct);

        var espelhoId = EspelhoDaSolicitacao(s);
        return await MontarAsync(new Alvo(s.SistemaDestino, s.SolicitacaoId, espelhoId, s.PacienteId, s.CriadoEm), ct);
    }

    public async Task<ConteudoAcervo> ObterGuiaAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", solicitacaoId);
        await escopo.ExigirAlcanceAsync(s, ct);
        // SISREG: a guia é da solicitação (espinha); os demais, do pedido no espelho.
        if (s.SistemaDestino == SistemaRegulacao.Sisreg && s.SolicitacaoId is { } espinhaId)
        {
            return await guias.ConteudoPorSolicitacaoAsync(espinhaId, ct);
        }
        if (s.SistemaDestino is not { } sistema || EspelhoDaSolicitacao(s) is not { } espelhoId)
        {
            throw new NaoEncontradoException("Guia do agendamento", solicitacaoId);
        }
        return await guias.ConteudoPorEspelhoAsync(sistema, espelhoId, ct);
    }

    public Task<ConteudoAcervo> ObterGuiaPorEspelhoAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct) =>
        guias.ConteudoPorEspelhoAsync(sistema, espelhoId, ct);

    private static Guid? EspelhoDaSolicitacao(Data.Entities.Regulacao.RegulacaoSolicitacao s) => s.SistemaDestino switch
    {
        SistemaRegulacao.Ser => s.SerSolicitacaoId,
        SistemaRegulacao.Sernit => s.SernitSolicitacaoId,
        SistemaRegulacao.EsusSg => s.EsusSgSolicitacaoId,
        _ => null,
    };

    public async Task<ContatoPacienteRegulacaoDto> ObterPorEspelhoAsync(
        SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct)
    {
        var linha = sistema switch
        {
            SistemaRegulacao.Ser => await db.SerSolicitacoes.AsNoTracking().Where(x => x.Id == espelhoId)
                .Select(x => new { x.PacienteId, x.DataSolicitacao, x.CriadoEm }).FirstOrDefaultAsync(ct),
            SistemaRegulacao.Sernit => await db.SernitSolicitacoes.AsNoTracking().Where(x => x.Id == espelhoId)
                .Select(x => new { x.PacienteId, x.DataSolicitacao, x.CriadoEm }).FirstOrDefaultAsync(ct),
            SistemaRegulacao.EsusSg => await db.EsusSgSolicitacoes.AsNoTracking().Where(x => x.Id == espelhoId)
                .Select(x => new { x.PacienteId, x.DataSolicitacao, x.CriadoEm }).FirstOrDefaultAsync(ct),
            _ => null,
        } ?? throw new NaoEncontradoException("Pedido do sistema de regulação", espelhoId);

        // Pedido ainda sem paciente conciliado: não há com quem ter falado.
        if (linha.PacienteId is not { } pacienteId)
            return new ContatoPacienteRegulacaoDto(
                await AgendamentoAsync(sistema, null, espelhoId, ct), await AvisaSozinhoAsync(sistema, ct),
                null, null, null, null, [], [], [], null);

        // Desde quando o pedido existe no sistema de destino — ou desde que o espelho o viu.
        var desde = linha.DataSolicitacao is { } d
            ? FusoBrasilia.DeBrasiliaParaUtc(d.ToDateTime(TimeOnly.MinValue))
            : linha.CriadoEm;
        return await MontarAsync(new Alvo(sistema, null, espelhoId, pacienteId, desde), ct);
    }

    private async Task<ContatoPacienteRegulacaoDto> MontarAsync(Alvo alvo, CancellationToken ct)
    {
        var agendamento = await AgendamentoAsync(alvo.Sistema, alvo.EspinhaId, alvo.EspelhoId, ct);

        IReadOnlyList<HistoricoComunicacaoDto> avisos = [];
        IReadOnlyList<HistoricoContatoDto> contatos = [];
        string? confirmacao = null;
        DateTime? confirmadoEm = null;
        string? canal = null;
        string? motivoCancelamento = null;

        if (alvo.EspinhaId is { } espinhaId)
        {
            // SISREG: aviso e contatos registrados vivem na espinha.
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
        else if (alvo.EspelhoId is { } espelhoId && alvo.Sistema is { } sistema)
        {
            // SER/SERNIT/ESUS SG: os avisos de todos os agendamentos deste pedido (a remarcação gera
            // aviso novo — o anterior fica na lista, de histórico) e a resposta do agendamento vigente.
            avisos = await db.ComunicacoesPaciente.AsNoTracking()
                .Where(c => c.AgendamentoExterno != null && c.AgendamentoExterno.OrigemId == espelhoId
                            && c.AgendamentoExterno.Sistema == sistema)
                .OrderBy(c => c.CriadoEm)
                .Select(c => new HistoricoComunicacaoDto(
                    c.Id, c.Finalidade.ToString(), c.Status.ToString(), c.Telefone, c.Tentativas, c.CriadoEm,
                    c.EnviadoEm, c.EntregueEm, c.LidoEm, c.VisualizadoEm, c.MotivoFalha,
                    c.MensagemWhatsApp != null ? c.MensagemWhatsApp.ErroMeta : null))
                .ToListAsync(ct);

            var vigente = await db.AgendamentosExternos.AsNoTracking()
                .Where(a => a.OrigemId == espelhoId && a.Sistema == sistema)
                .OrderBy(a => a.Situacao == SituacaoAgendamentoExterno.Ativo ? 0 : 1)
                .ThenByDescending(a => a.CriadoEm)
                .FirstOrDefaultAsync(ct);
            if (vigente is not null && (avisos.Count > 0 || vigente.ConfirmadoCanal != null))
            {
                confirmacao = vigente.StatusConfirmacao.ToString();
                confirmadoEm = vigente.ConfirmadoEm ?? vigente.ConfirmacaoCanceladaEm;
                canal = vigente.ConfirmadoCanal;
                motivoCancelamento = vigente.MotivoCancelamentoPaciente;
            }
        }

        var mensagens = await MensagensAsync(alvo, ct);

        return new ContatoPacienteRegulacaoDto(
            agendamento,
            alvo.Sistema is { } sis && await AvisaSozinhoAsync(sis, ct),
            confirmacao, confirmadoEm, canal, motivoCancelamento,
            avisos, contatos, mensagens,
            await TelefoneConfirmadoAsync(alvo.PacienteId, ct),
            await GuiaAsync(alvo, ct));
    }

    /// <summary>A guia do agendamento (ADR-0076): da solicitação no SISREG, do pedido no espelho nos demais.</summary>
    private async Task<GuiaDoAgendamentoDto?> GuiaAsync(Alvo alvo, CancellationToken ct) => alvo switch
    {
        { Sistema: SistemaRegulacao.Sisreg, EspinhaId: { } espinha } => await guias.SituacaoPorSolicitacaoAsync(espinha, ct),
        { Sistema: { } sistema, EspelhoId: { } espelho } when sistema != SistemaRegulacao.Sisreg =>
            await guias.SituacaoPorEspelhoAsync(sistema, espelho, ct),
        _ => null,
    };

    /// <summary>O SISREG avisa sempre; o SER/SERNIT/ESUS SG, enquanto a chave da Mensageria estiver ligada.</summary>
    private async Task<bool> AvisaSozinhoAsync(SistemaRegulacao sistema, CancellationToken ct) =>
        sistema switch
        {
            SistemaRegulacao.Sisreg => true,
            SistemaRegulacao.Ser or SistemaRegulacao.Sernit or SistemaRegulacao.EsusSg =>
                (await regras.ObterAsync(ct)).AvisoAgendamentoExternoHabilitado,
            _ => false,
        };

    private async Task<IReadOnlyList<MensagemAoPacienteDto>> MensagensAsync(Alvo alvo, CancellationToken ct)
    {
        var linhas = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.PacienteId == alvo.PacienteId && m.OcorridoEm >= alvo.Desde
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
                Procedimento = c.Solicitacao != null ? c.Solicitacao.ProcedimentoTexto : null,
                DataAgendada = c.Solicitacao != null ? c.Solicitacao.DataAgendada : null,
                Externo = c.AgendamentoExterno,
            })
            .ToListAsync(ct);
        var avisoPorMensagem = avisosDasMensagens
            .GroupBy(a => a.MensagemId)
            .ToDictionary(g => g.Key, g =>
            {
                var a = g.First();
                var deste = a.Externo is { } e
                    ? alvo.EspelhoId == e.OrigemId && alvo.Sistema == e.Sistema
                    : a.SolicitacaoId != null && a.SolicitacaoId == alvo.EspinhaId;
                return new AvisoDaMensagemDto(
                    a.Finalidade.ToString(), deste,
                    a.Procedimento ?? a.Externo?.Procedimento,
                    a.DataAgendada ?? (a.Externo is { } x ? ComunicacaoPacienteService.InstanteDoAgendamento(x) : null));
            });

        return linhas
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
    }

    private async Task<AgendamentoNoDestinoDto?> AgendamentoAsync(
        SistemaRegulacao? sistema, Guid? espinhaId, Guid? espelhoId, CancellationToken ct)
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
        else if (sistema == SistemaRegulacao.Ser && espelhoId is { } ser)
        {
            a = await db.SerSolicitacoes.AsNoTracking()
                .Where(x => x.Id == ser)
                .Select(x => new AgendamentoNoDestinoDto(
                    SistemaRegulacao.Ser, x.Situacao.ToString(), null, x.AgendadoParaTexto, x.UnidadeExecutora))
                .FirstOrDefaultAsync(ct);
        }
        else if (sistema == SistemaRegulacao.Sernit && espelhoId is { } sernit)
        {
            a = await db.SernitSolicitacoes.AsNoTracking()
                .Where(x => x.Id == sernit)
                .Select(x => new AgendamentoNoDestinoDto(
                    SistemaRegulacao.Sernit, x.Situacao.ToString(), null, x.AgendadoParaTexto, x.UnidadeExecutora))
                .FirstOrDefaultAsync(ct);
        }
        else if (sistema == SistemaRegulacao.EsusSg && espelhoId is { } esus)
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
        if (a is null || (a.DataHora is null && string.IsNullOrWhiteSpace(a.DataTexto))) return null;

        // O SERNIT deixa a unidade executora vazia e escreve o local junto da data: o quadro mostra o
        // mesmo local que foi no aviso.
        if (a.Local is null && a.Sistema != SistemaRegulacao.Sisreg && espelhoId is { } origem)
        {
            var localDoAviso = await db.AgendamentosExternos.AsNoTracking()
                .Where(x => x.OrigemId == origem && x.Sistema == a.Sistema && x.Situacao == SituacaoAgendamentoExterno.Ativo)
                .Select(x => x.LocalNome)
                .FirstOrDefaultAsync(ct);
            a = a with
            {
                Local = localDoAviso
                    ?? (a.Sistema == SistemaRegulacao.Sernit ? LeituraAgendamentoExterno.DataHora(a.DataTexto).Resto : null),
            };
        }
        return a;
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
