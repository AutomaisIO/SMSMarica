using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using SMSMais.Core.Alertas;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Notificacoes.Campanhas;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>O que uma passada do aviso de remarcação fez — vai para o log.</summary>
public sealed record ResultadoAvisoRemarcacao(int Avisados, int SaemComDataNova, int SemAviso, int ParaEquipe, int Retidos);

/// <summary>O "Avisar paciente" preparado: a comunicação que sai agora (se houver) e a frase do que foi feito.</summary>
public sealed record AvisoManualPreparado(Guid? ComunicacaoId, string Nota);

public interface IAvisoRemarcacaoService
{
    /// <summary>
    /// Passada do automático: decide, para cada remarcação do SISREG que ainda não olhou, se o
    /// paciente recebe a confirmação de novo com a data nova, se a mensagem que já ia sair basta, ou
    /// se a remarcação fica para a equipe (e por quê). Salva.
    /// </summary>
    Task<ResultadoAvisoRemarcacao> ProcessarPendentesAsync(CancellationToken ct = default);

    /// <summary>
    /// "Avisar paciente" da fila de Alterações da agenda — decisão de uma pessoa, então passa por
    /// cima do que o automático respeita (chave, unidade sem aviso, "não vou", atendimento humano).
    /// Reabre a confirmação e põe a mensagem com os dados de agora na fila. NÃO salva: o chamador
    /// marca a alteração, salva e dispara <see cref="AvisoManualPreparado.ComunicacaoId"/>.
    /// </summary>
    /// <param name="remarcacao">A linha da fila de onde partiu o aviso. Remarcação de data faz quem já
    /// conhecia a data antiga receber o aviso de mudança de data.</param>
    Task<AvisoManualPreparado> PrepararAvisoManualAsync(
        Guid solicitacaoId, SisregAlteracaoAgenda? remarcacao = null, CancellationToken ct = default);
}

/// <summary>
/// Aviso ao paciente quando o SISREG <b>remarca</b> um agendamento que já estava aqui (23/10/2026).
///
/// <para><b>O furo que fecha.</b> A sincronização diária relê a agenda, acha a data nova, aplica e
/// registra em <c>sisreg_alteracao_agenda</c> — e parava aí. Quem tinha recebido a data antiga
/// ficava com ela; quem tinha confirmado seguia "Confirmado" para um horário que não existia mais.
/// O botão "Avisar paciente" da fila deveria cobrir isso e nunca funcionou.</para>
///
/// <para><b>Por que um passo à parte, e não dentro da importação.</b> A importação vê uma linha
/// por vez e não sabe se aquela é a 1ª de 3 remarcações ou a 1ª de 2.000 — que é o que uma leitura
/// errada do SISREG produz (06/09/2026: 1.378 linhas na fila, ~34 reais). Aqui a decisão espera a
/// leitura da unidade terminar e olha o conjunto antes de avisar qualquer um: com volume de leitura
/// errada, segura a unidade inteira e avisa no celular. Avisar um paciente de uma data errada é a
/// única mensagem sem conserto.</para>
///
/// <para><b>A mensagem é a própria confirmação</b>, montada na hora do envio com a data de agora: o
/// paciente responde de novo, e a resposta anterior (era sobre a data antiga) deixa de valer. Não
/// há modelo "foi remarcado" aprovado na Meta; quando houver, entra aqui.</para>
///
/// <para>Só ENFILEIRA. Quem envia é o <see cref="EnviadorComunicacaoService"/>, com a janela de
/// horário, a vazão e as guardas de LGPD de sempre.</para>
/// </summary>
public sealed class AvisoRemarcacaoService(
    SmsMaisDbContext db,
    IConfirmacaoConfiguracaoService regras,
    IComunicacaoPacienteService comunicacoes,
    IWhatsAppCliente whatsApp,
    IOptions<ComunicacaoPacienteOptions> opcoes,
    IAlertaPlataforma alerta,
    ILogger<AvisoRemarcacaoService> logger) : IAvisoRemarcacaoService
{
    /// <summary>Carimbo na <c>motivo_falha</c> do lembrete dispensado por remarcação — é o que deixa o
    /// <see cref="LembreteAgendamentoService"/> rearmá-lo para a data nova.</summary>
    public const string CarimboLembreteRemarcado = "Remarcado no SISREG";

    /// <summary>A alteração espera um pouco antes de ser olhada: a mesma leitura pode trazer, logo
    /// depois, outra linha do mesmo pedido.</summary>
    internal static readonly TimeSpan Assentamento = TimeSpan.FromMinutes(5);

    /// <summary>Leitura da unidade sem sinal de vida há mais que isto é tratada como morta — não
    /// segura o aviso para sempre.</summary>
    internal static readonly TimeSpan LeituraViva = TimeSpan.FromMinutes(15);

    /// <summary>
    /// O freio. Remarcação de verdade é pontual — a agenda de um profissional que mudou, algumas
    /// dezenas de pacientes numa unidade de milhares. Leitura errada (fuso, campo trocado, arquivo
    /// truncado) mexe em quase tudo. Por isso a régua é dupla: pelo menos
    /// <see cref="FreioMinimo"/> remarcações em <see cref="JanelaFreio"/> <b>e</b> mais de
    /// <see cref="FreioProporcao"/> da agenda futura da unidade. Unidade pequena que teve a agenda
    /// inteira mudada cai no freio também — e aí a equipe avisa pelo botão, que é o lado seguro.
    /// </summary>
    internal const int FreioMinimo = 20;
    internal const double FreioProporcao = 0.10;
    internal static readonly TimeSpan JanelaFreio = TimeSpan.FromHours(24);

    /// <summary>Data que volta para uma que já tinha sido trocada, dentro disto, é vai-e-volta: pode
    /// ser duas leituras discordando, e mandar duas mensagens com datas alternadas é pior que nenhuma.</summary>
    internal static readonly TimeSpan JanelaVaiEVolta = TimeSpan.FromDays(30);

    private const int MaximoPorPassagem = 500;

    public async Task<ResultadoAvisoRemarcacao> ProcessarPendentesAsync(CancellationToken ct = default)
    {
        var cfg = await regras.ObterAsync(ct);
        if (!cfg.AvisoRemarcacaoHabilitado) return new(0, 0, 0, 0, 0);

        var agora = DateTime.UtcNow;
        var assentada = agora - Assentamento;
        var corte = cfg.AvisoRemarcacaoLigadoEm;

        var pendentes = await db.SisregAlteracoesAgenda.AsNoTracking()
            .Where(a => a.Tipo == TipoAlteracaoAgenda.DataHora
                && a.AvisoAutomaticoEm == null
                && a.TratadaEm == null
                && a.DetectadaEm <= assentada
                && (corte == null || a.DetectadaEm >= corte))
            .OrderBy(a => a.DetectadaEm)
            .Take(MaximoPorPassagem)
            .Select(a => new { a.Id, a.SolicitacaoId, a.UnidadeExecutanteId, a.DetectadaEm })
            .ToListAsync(ct);
        if (pendentes.Count == 0) return new(0, 0, 0, 0, 0);

        // A leitura da unidade ainda está correndo: o conjunto não está completo, e é ele que o
        // freio precisa ver. Fica para a próxima passada.
        var vivaDesde = agora - LeituraViva;
        var emLeitura = (await db.SisregVarreduraExecucoes.AsNoTracking()
                .Where(e => (e.Status == StatusVarredura.Pendente || e.Status == StatusVarredura.EmExecucao)
                    && (e.UltimoSinalEm ?? e.IniciadoEm) > vivaDesde)
                .Select(e => e.UnidadeId)
                .ToListAsync(ct))
            .ToHashSet();

        int avisados = 0, saem = 0, semAviso = 0, equipe = 0, retidos = 0;

        foreach (var daUnidade in pendentes.GroupBy(a => a.UnidadeExecutanteId))
        {
            if (daUnidade.Key is { } unidadeId && emLeitura.Contains(unidadeId)) continue;

            if (daUnidade.Key is { } u && await FreioAsync(u, agora, ct) is { } freio)
            {
                await ReterAsync(daUnidade.Select(a => a.Id).ToList(), freio, agora, ct);
                retidos += daUnidade.Count();
                logger.LogWarning("SISREG_REMARCACAO_RETIDA: unidade {Unidade} — {Detalhe}", u, freio);
                var nome = await db.Unidades.AsNoTracking().Where(x => x.Id == u).Select(x => x.Nome).FirstOrDefaultAsync(ct);
                alerta.Reportar(new EventoAlerta(AlertaCatalogo.SisregRemarcacaoRetida,
                    $"remarcações retidas — {nome ?? "unidade"}",
                    $"{freio} Nenhum paciente foi avisado: as remarcações estão em Alterações da agenda para "
                    + "a equipe conferir no SISREG e avisar pelo botão.")
                {
                    Rotulo = "Remarcações do SISREG retidas",
                    Grupo = "Sincronismo",
                });
                continue;
            }

            foreach (var daSolicitacao in daUnidade.GroupBy(a => a.SolicitacaoId))
            {
                try
                {
                    var desfecho = await ProcessarSolicitacaoAsync(
                        daSolicitacao.Key, daSolicitacao.Select(a => a.Id).ToList(), agora, ct);
                    switch (desfecho)
                    {
                        case DesfechoRemarcacao.Avisar: avisados++; break;
                        case DesfechoRemarcacao.SaiComDataNova: saem++; break;
                        case DesfechoRemarcacao.NadaAAvisar: semAviso++; break;
                        case DesfechoRemarcacao.Retida: retidos++; break;
                        default: equipe++; break;
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    // O paciente respondeu (ou a importação mexeu) no mesmo instante: nada foi gravado,
                    // e a alteração continua sem marca — a próxima passada refaz com o estado novo.
                    db.ChangeTracker.Clear();
                }
            }
        }

        if (avisados + saem + semAviso + equipe + retidos > 0)
            logger.LogInformation(
                "Remarcações do SISREG: {Avisados} paciente(s) avisado(s) com a data nova, {Saem} já iam receber a "
                + "data nova, {SemAviso} sem o que avisar, {Equipe} para a equipe, {Retidos} retida(s).",
                avisados, saem, semAviso, equipe, retidos);

        return new(avisados, saem, semAviso, equipe, retidos);
    }

    /// <summary>Uma solicitação: decide sobre a remarcação mais recente e encerra as anteriores. Salva.</summary>
    private async Task<DesfechoRemarcacao> ProcessarSolicitacaoAsync(
        Guid solicitacaoId, IReadOnlyList<Guid> idsDaPassada, DateTime agora, CancellationToken ct)
    {
        // Inclui as remarcações anteriores ainda abertas da mesma solicitação: depois da mais nova,
        // elas falam de uma data que não vale mais — na fila, só confundiriam quem for ligar.
        var alteracoes = await db.SisregAlteracoesAgenda
            .Where(a => a.SolicitacaoId == solicitacaoId && a.Tipo == TipoAlteracaoAgenda.DataHora
                && (idsDaPassada.Contains(a.Id) || a.TratadaEm == null))
            .OrderByDescending(a => a.DetectadaEm)
            .ToListAsync(ct);
        var ultima = alteracoes.FirstOrDefault(a => idsDaPassada.Contains(a.Id));
        if (ultima is null) return DesfechoRemarcacao.Equipe;
        // Uma remarcação mais nova já olhada (e devolvida à equipe) manda: esta ficou para trás.
        if (alteracoes[0] != ultima)
        {
            Marcar(ultima, agora, "Superada: o SISREG mudou a data de novo depois desta.", tratar: true);
            await db.SaveChangesAsync(ct);
            return DesfechoRemarcacao.NadaAAvisar;
        }
        foreach (var anterior in alteracoes.Skip(1))
            Marcar(anterior, agora, "Superada: o SISREG mudou a data de novo depois desta.", tratar: true);

        var vaiEVolta = await db.SisregAlteracoesAgenda.AsNoTracking().AnyAsync(a =>
            a.SolicitacaoId == solicitacaoId && a.Tipo == TipoAlteracaoAgenda.DataHora && a.Id != ultima.Id
            && a.ValorAntes == ultima.ValorDepois && a.DetectadaEm >= agora - JanelaVaiEVolta, ct);
        if (vaiEVolta)
        {
            Marcar(ultima, agora,
                "Retida: a data voltou para uma que já tinha sido trocada (vai e volta). Confira no SISREG "
                + "antes de avisar o paciente.", tratar: false);
            await db.SaveChangesAsync(ct);
            return DesfechoRemarcacao.Retida;
        }

        var s = await db.Solicitacoes.FirstOrDefaultAsync(x => x.Id == solicitacaoId, ct);
        if (s is null)
        {
            Marcar(ultima, agora, "A solicitação não existe mais.", tratar: true);
            await db.SaveChangesAsync(ct);
            return DesfechoRemarcacao.NadaAAvisar;
        }

        var confirmacao = await ConfirmacaoDaAsync(s.Id, ct);
        var fatos = new FatosRemarcacao(
            DataNovaEhAAtual: s.DataAgendada is not null
                && ultima.ValorDepois == ComparadorMarcacao.FormatarData(s.DataAgendada),
            SolicitacaoEncerrada: s.ExcluidoEm is not null
                || s.Status is StatusSolicitacao.Cancelada or StatusSolicitacao.Realizada
                || s.AutorizadoEm is not null,
            DataNovaUtc: s.DataAgendada ?? DateTime.MinValue,
            Confirmacao: s.StatusConfirmacao,
            UnidadeAvisa: await UnidadeAvisaAsync(s, ct),
            EmAtendimentoHumano: await db.AtendimentosConfirmacao.AsNoTracking()
                .AnyAsync(a => a.SolicitacaoId == s.Id && a.EncerradoEm == null, ct),
            Comunicacao: confirmacao?.Status);

        var decisao = Decidir(fatos, agora);
        var desfecho = decisao.Desfecho;
        var nota = decisao.Nota;
        // Lido ANTES de reabrir a confirmação: "tinha confirmado" é uma das provas de que o paciente
        // conhecia a data antiga.
        var dataQueConhecia = await DataQueOPacienteConheciaAsync(s, confirmacao, ultima, ct);
        var dataAntigaSaiuEm = confirmacao is
        {
            EnviadoEm: { } saiu,
            Status: StatusComunicacao.Enviada or StatusComunicacao.Entregue or StatusComunicacao.Lida,
        } ? saiu : (DateTime?)null;

        if (desfecho == DesfechoRemarcacao.Avisar)
        {
            if (confirmacao is null)
            {
                await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
                confirmacao = ConfirmacaoNova(s.Id);
                if (confirmacao is null)
                {
                    // A regra "só SISREG" (ou outra guarda do enfileiramento) não deixou: não há o que
                    // prometer, e a remarcação volta para quem pode ligar.
                    desfecho = DesfechoRemarcacao.Equipe;
                    nota = "A confirmação automática não vale para esta solicitação: avise por telefone.";
                }
            }
            else
            {
                if (confirmacao.EnviadoEm is { } enviadaEm)
                    nota = nota.TrimEnd('.') + $" (a da data antiga saiu em {Quando(enviadaEm)}).";
                ComunicacaoPacienteService.RearmarParaNovoEnvio(confirmacao);
            }
        }

        // Quem já conhecia a data antiga recebe primeiro o aviso de MUDANÇA DE DATA (sem dado nenhum) e
        // a data nova ao tocar em "Quero mais informações" — a confirmação comum leria como se nada
        // tivesse mudado. A primeira mensagem que esperava a identificação não dizia que a data mudou
        // (nem dizia data): sai de novo, agora como aviso de mudança.
        if (desfecho is DesfechoRemarcacao.Avisar or DesfechoRemarcacao.SaiComDataNova
            && confirmacao is not null && dataQueConhecia is { } de)
        {
            confirmacao.RemarcadaDe = de;
            if (desfecho == DesfechoRemarcacao.SaiComDataNova
                && confirmacao.Status == StatusComunicacao.AguardandoVerificacaoCadastral)
            {
                ComunicacaoPacienteService.RearmarParaNovoEnvio(confirmacao);
                desfecho = DesfechoRemarcacao.Avisar;
            }
            nota = desfecho == DesfechoRemarcacao.Avisar
                ? "Aviso de mudança de data na fila, com a data anterior e a nova"
                  + (dataAntigaSaiuEm is { } saiuEm ? $" (a mensagem da data antiga saiu em {Quando(saiuEm)})." : ".")
                : "A mensagem ainda não tinha saído: sai como aviso de mudança de data.";

            // Sem os modelos aprovados na Meta a mensagem espera na fila (o envio segura) — e a
            // remarcação fica visível para a equipe enquanto isso.
            if (await ModelosDeMudancaAprovadosAsync(ct) == false)
            {
                desfecho = DesfechoRemarcacao.Equipe;
                nota = "Na fila como aviso de mudança de data, mas os modelos ainda não estão aprovados como "
                    + "utilidade na Meta: a mensagem sai sozinha quando estiverem. Se o atendimento estiver perto, "
                    + "avise por telefone.";
            }
        }

        // Depois do enfileiramento de propósito: nenhum dos dois olha a resposta, e a frase sobre o
        // que deixou de valer precisa ler a resposta antes de ela ser apagada.
        if (decisao.ReabreConfirmacao)
        {
            nota += $" {RespostaAnterior(s)} deixou de valer: era sobre a data antiga.";
            ReabrirConfirmacao(s, agora, incluindoNaoVou: false);
        }

        if (decisao.Desfecho is DesfechoRemarcacao.Avisar or DesfechoRemarcacao.SaiComDataNova)
            await DispensarLembreteAsync(s.Id, agora, ct);

        Marcar(ultima, agora, nota, tratar: desfecho is not DesfechoRemarcacao.Equipe);
        if (desfecho == DesfechoRemarcacao.Avisar) ultima.ComunicadaEm = agora;

        await db.SaveChangesAsync(ct);
        return desfecho;
    }

    public async Task<AvisoManualPreparado> PrepararAvisoManualAsync(
        Guid solicitacaoId, SisregAlteracaoAgenda? remarcacao = null, CancellationToken ct = default)
    {
        var s = await db.Solicitacoes.FirstOrDefaultAsync(x => x.Id == solicitacaoId, ct)
            ?? throw new NaoEncontradoException(nameof(Solicitacao), solicitacaoId);

        var agora = DateTime.UtcNow;
        if (s.ExcluidoEm is not null || s.Status is StatusSolicitacao.Cancelada or StatusSolicitacao.Realizada)
            throw new ConflitoException("aviso.solicitacao_encerrada",
                "A solicitação foi cancelada ou já realizada — não há o que avisar.");
        if (s.DataAgendada is not { } quando || quando <= agora)
            throw new ConflitoException("aviso.sem_data_futura",
                "A data do agendamento já passou — não há o que avisar.");

        var c = await ConfirmacaoDaAsync(s.Id, ct);
        // Antes de reabrir: "tinha confirmado" é prova de que o paciente conhecia a data antiga.
        var dataQueConhecia = remarcacao is { Tipo: TipoAlteracaoAgenda.DataHora }
            ? await DataQueOPacienteConheciaAsync(s, c, remarcacao, ct)
            : null;
        var deixouDeValer = s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
            ? string.Empty
            : $" {RespostaAnterior(s)} deixou de valer.";
        ReabrirConfirmacao(s, agora, incluindoNaoVou: true);

        Guid? enviarAgora = null;
        string nota;
        if (c is null)
        {
            await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
            enviarAgora = ConfirmacaoNova(s.Id)?.Id
                ?? throw new ConflitoException("aviso.confirmacao_nao_vale",
                    "A confirmação por WhatsApp não vale para esta solicitação (regra \"só SISREG\" em "
                    + "Mensageria → Regras). Avise por telefone.");
            nota = "Confirmação enviada pela equipe com a data de agora.";
        }
        else if (c.Status is StatusComunicacao.AguardandoVerificacaoCadastral && dataQueConhecia is null)
        {
            nota = "A primeira mensagem já pediu que o paciente se identifique: a data nova sai assim que ele responder.";
        }
        else if (c.Status is StatusComunicacao.AguardandoCorrecaoContato)
        {
            // Número que alguém disse não ser do paciente: mandar de novo é a mensagem para a pessoa errada.
            nota = "O número do cadastro foi negado por quem atendeu: corrija o contato e a confirmação sai sozinha.";
        }
        else
        {
            if (c.Status != StatusComunicacao.Pendente) ComunicacaoPacienteService.RearmarParaNovoEnvio(c);
            enviarAgora = c.Id;
            nota = "Confirmação reenviada pela equipe com a data de agora.";
        }

        c ??= ConfirmacaoNova(s.Id);
        if (c is not null && dataQueConhecia is { } de)
        {
            c.RemarcadaDe = de;
            nota += await ModelosDeMudancaAprovadosAsync(ct) == false
                ? " Vai como aviso de mudança de data assim que os modelos estiverem aprovados como utilidade na Meta."
                : " Vai como aviso de mudança de data, com a data anterior e a nova.";
        }

        await DispensarLembreteAsync(s.Id, agora, ct);
        return new(enviarAgora, nota + deixouDeValer);
    }

    // ---------------- decisão (pura, testada sem banco) ----------------

    internal enum DesfechoRemarcacao
    {
        /// <summary>A confirmação sai de novo, com a data nova.</summary>
        Avisar,
        /// <summary>A mensagem da data antiga ainda não tinha saído: sai já com a nova.</summary>
        SaiComDataNova,
        /// <summary>Não há o que avisar (solicitação encerrada, data passada, remarcação superada).</summary>
        NadaAAvisar,
        /// <summary>Fica na fila para a equipe, com o motivo.</summary>
        Equipe,
        /// <summary>Segurada pelo freio ou pelo vai-e-volta — também na fila da equipe.</summary>
        Retida,
    }

    internal sealed record FatosRemarcacao(
        bool DataNovaEhAAtual,
        bool SolicitacaoEncerrada,
        DateTime DataNovaUtc,
        StatusConfirmacaoAgendamento Confirmacao,
        bool UnidadeAvisa,
        bool EmAtendimentoHumano,
        StatusComunicacao? Comunicacao);

    internal sealed record DecisaoRemarcacao(DesfechoRemarcacao Desfecho, string Nota, bool ReabreConfirmacao);

    /// <summary>
    /// O que fazer com o paciente diante da data nova. A ordem é a das perguntas que uma atendente
    /// faria: a data é mesmo esta? ainda há atendimento? o paciente já disse que não vai? dá tempo de
    /// mensagem? a unidade avisa por WhatsApp? tem gente cuidando? e só então, o que a mensagem
    /// anterior fez.
    /// </summary>
    internal static DecisaoRemarcacao Decidir(FatosRemarcacao f, DateTime agoraUtc)
    {
        if (!f.DataNovaEhAAtual)
            return new(DesfechoRemarcacao.Equipe,
                "A data nova não é a que a solicitação mostra hoje (outro procedimento do mesmo pedido?): "
                + "avise pelo botão ou por telefone.", false);

        if (f.SolicitacaoEncerrada)
            return new(DesfechoRemarcacao.NadaAAvisar,
                "A solicitação já foi cancelada, realizada ou recebida na recepção: não há o que avisar.", false);

        if (f.DataNovaUtc <= agoraUtc)
            return new(DesfechoRemarcacao.NadaAAvisar, "A data nova já passou: não há o que avisar.", false);

        // "Não vou" foi a resposta sobre a data antiga — talvez a remarcação seja justamente a
        // resposta da regulação a isso. Reabrir sozinho apagaria o pedido de cancelamento da fila.
        if (f.Confirmacao == StatusConfirmacaoAgendamento.Cancelada)
            return new(DesfechoRemarcacao.Equipe,
                "O paciente tinha avisado que não iria na data antiga. Confirme com ele se a data nova serve "
                + "(\"Avisar paciente\" reabre a confirmação).", false);

        var reabre = f.Confirmacao == StatusConfirmacaoAgendamento.Confirmada;

        if (FusoBrasilia.ParaExibicao(f.DataNovaUtc).Date == FusoBrasilia.ParaExibicao(agoraUtc).Date)
            return new(DesfechoRemarcacao.Equipe,
                "Remarcado para hoje: avise por telefone — a mensagem automática não chegaria a tempo.", reabre);

        if (!f.UnidadeAvisa)
            return new(DesfechoRemarcacao.Equipe,
                "A unidade ou o procedimento não avisa por WhatsApp: avise por telefone.", reabre);

        // ADR-0059: depois que uma pessoa entrou no circuito, o automático não fala mais com o paciente.
        if (f.EmAtendimentoHumano || f.Comunicacao == StatusComunicacao.SubstituidaPorAtendente)
            return new(DesfechoRemarcacao.Equipe,
                "Em atendimento humano (Confirmações): o aviso fica com quem atende.", reabre);

        return f.Comunicacao switch
        {
            null => new(DesfechoRemarcacao.Avisar,
                "Confirmação com a data nova na fila — é a primeira mensagem a este paciente.", reabre),
            StatusComunicacao.Pendente
                or StatusComunicacao.AguardandoVerificacaoCadastral
                or StatusComunicacao.AguardandoTelefoneVerificado
                or StatusComunicacao.AguardandoCorrecaoContato => new(DesfechoRemarcacao.SaiComDataNova,
                "A confirmação ainda não tinha levado a data antiga ao paciente: quando sair, sai com a nova.", reabre),
            StatusComunicacao.Falha or StatusComunicacao.SemTelefoneValido => new(DesfechoRemarcacao.Equipe,
                "A mensagem da data antiga não chegou ao paciente: avise por telefone.", reabre),
            _ => new(DesfechoRemarcacao.Avisar, "Confirmação reenviada com a data nova.", reabre),
        };
    }

    // ---------------- apoio ----------------

    /// <summary>Freio da unidade: o motivo, ou null quando o volume é de remarcação de verdade.</summary>
    private async Task<string?> FreioAsync(Guid unidadeId, DateTime agora, CancellationToken ct)
    {
        var desde = agora - JanelaFreio;
        var remarcacoes = await db.SisregAlteracoesAgenda.AsNoTracking()
            .CountAsync(a => a.UnidadeExecutanteId == unidadeId && a.Tipo == TipoAlteracaoAgenda.DataHora
                && a.DetectadaEm >= desde, ct);
        if (remarcacoes < FreioMinimo) return null;

        var agendaFutura = await db.Solicitacoes.AsNoTracking()
            .CountAsync(s => s.UnidadeExecutanteId == unidadeId && s.ExcluidoEm == null
                && s.Status != StatusSolicitacao.Cancelada && s.DataAgendada > agora, ct);
        if (remarcacoes <= agendaFutura * FreioProporcao) return null;

        return $"{remarcacoes} remarcações em 24 h, contra {agendaFutura} agendamentos futuros na unidade — "
            + "volume de leitura errada, não de remarcação.";
    }

    private async Task ReterAsync(IReadOnlyList<Guid> ids, string motivo, DateTime agora, CancellationToken ct)
    {
        var alteracoes = await db.SisregAlteracoesAgenda.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        foreach (var a in alteracoes)
            Marcar(a, agora, $"Retida: {motivo} Confira no SISREG antes de avisar.", tratar: false);
        await db.SaveChangesAsync(ct);
    }

    private static void Marcar(SisregAlteracaoAgenda a, DateTime agora, string nota, bool tratar)
    {
        a.AvisoAutomaticoEm = agora;
        a.NotaAviso = nota.Length <= 500 ? nota : nota[..500];
        if (tratar) a.TratadaEm ??= agora;
    }

    /// <summary>Mesma régua da primeira mensagem (importação) e do lembrete: campanha em envio
    /// automático, ou unidade E procedimento com o aviso ligado.</summary>
    private async Task<bool> UnidadeAvisaAsync(Solicitacao s, CancellationToken ct)
    {
        if (await CampanhaResolver.VigenteAsync(db, s.UnidadeExecutanteId, s.DataAgendada, ct) is { EnvioAutomatico: true })
            return !await CampanhaResolver.ProcedimentoSilenciadoAsync(
                db, s.UnidadeExecutanteId, s.ProcedimentoCodigoSisreg, s.ProcedimentoTexto, ct);

        var unidadeLigada = await db.SisregVarreduraAgendas.AsNoTracking()
            .AnyAsync(a => a.UnidadeId == s.UnidadeExecutanteId && a.EnviarConfirmacao, ct);
        var codigo = s.ProcedimentoCodigoSisreg?.Trim();
        if (!unidadeLigada || string.IsNullOrEmpty(codigo)) return false;

        return await db.SisregProcedimentosProfissional.AsNoTracking()
            .AnyAsync(p => p.Codigo == codigo && p.Profissional!.UnidadeId == s.UnidadeExecutanteId
                && p.EnviarConfirmacao, ct);
    }

    /// <summary>
    /// A data que o paciente tinha na mão antes da remarcação — ou null, se ele nunca soube de data
    /// nenhuma (a mensagem com a data não tinha saído e ele não tinha confirmado). Sabia se: recebeu a
    /// confirmação com a data, tinha confirmado por qualquer canal, ou já esperava um aviso de
    /// remarcação que ainda não saiu (aí continua valendo a data daquele aviso).
    ///
    /// <para>Qual data: a de antes da primeira remarcação depois do momento em que ele soube —
    /// com duas remarcações seguidas antes de qualquer aviso, ele conhece a primeira, não a do meio.</para>
    /// </summary>
    private async Task<DateTime?> DataQueOPacienteConheciaAsync(
        Solicitacao s, ComunicacaoPaciente? confirmacao, SisregAlteracaoAgenda remarcacao, CancellationToken ct)
    {
        var levouAData = confirmacao is
        {
            EnviadoEm: not null,
            Status: StatusComunicacao.Enviada or StatusComunicacao.Entregue or StatusComunicacao.Lida,
        };
        var tinhaConfirmado = s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada;

        // Aviso de remarcação que ainda não saiu: o paciente segue com a data daquele aviso.
        if (!levouAData && confirmacao?.RemarcadaDe is { } aindaNaFila) return aindaNaFila;
        if (!levouAData && !tinhaConfirmado) return null;

        DateTime? desde = levouAData ? confirmacao!.EnviadoEm : null;
        if (tinhaConfirmado && s.ConfirmadoEm is { } confirmadoEm && (desde is null || confirmadoEm > desde))
            desde = confirmadoEm;

        var antes = desde is { } d
            ? await db.SisregAlteracoesAgenda.AsNoTracking()
                .Where(a => a.SolicitacaoId == s.Id && a.Tipo == TipoAlteracaoAgenda.DataHora && a.DetectadaEm >= d)
                .OrderBy(a => a.DetectadaEm)
                .Select(a => a.ValorAntes)
                .FirstOrDefaultAsync(ct)
            : null;
        return LerData(antes ?? remarcacao.ValorAntes);
    }

    /// <summary>"10/11/2026 08:00" (Brasília, o formato da fila) → UTC. Null se não for uma data só
    /// — a alteração de outro procedimento do pedido vem com o nome dele na frente.</summary>
    internal static DateTime? LerData(string? valor) =>
        DateTime.TryParseExact(valor, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? FusoBrasilia.DeBrasiliaParaUtc(local)
            : null;

    /// <summary>Os dois modelos de mudança de data (provado e anônimo) estão aprovados na Meta? Qual vai
    /// depende do número na hora do envio, então valem os dois. Null = não dá para saber (catálogo fora
    /// ou vazio) — segue como se estivessem.</summary>
    private async Task<bool?> ModelosDeMudancaAprovadosAsync(CancellationToken ct)
    {
        IReadOnlyList<TemplateWhatsApp> catalogo;
        try { catalogo = await whatsApp.ListarTemplatesAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
        if (catalogo.Count == 0) return null;
        // Aprovado como MARKETING não conta: o SMSMais não envia modelo assim (WhatsAppCliente).
        bool Aprovado(string nome) =>
            catalogo.Any(t => string.Equals(t.Nome, nome, StringComparison.OrdinalIgnoreCase) && !t.Marketing);
        return Aprovado(opcoes.Value.TemplateMudancaDeData) && Aprovado(opcoes.Value.TemplateMudancaDeDataAnonimo);
    }

    private Task<ComunicacaoPaciente?> ConfirmacaoDaAsync(Guid solicitacaoId, CancellationToken ct) =>
        db.ComunicacoesPaciente.FirstOrDefaultAsync(c => c.SolicitacaoId == solicitacaoId
            && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);

    /// <summary>A confirmação que o enfileiramento acabou de criar (ainda não salva), se criou.</summary>
    private ComunicacaoPaciente? ConfirmacaoNova(Guid solicitacaoId) =>
        db.ChangeTracker.Entries<ComunicacaoPaciente>()
            .Where(e => e.State == EntityState.Added && e.Entity.SolicitacaoId == solicitacaoId
                && e.Entity.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .Select(e => e.Entity)
            .FirstOrDefault();

    /// <summary>
    /// A resposta do paciente volta a "sem resposta". Confirmar era sobre a data antiga — e é
    /// "sem resposta" que faz a confirmação sair de novo (o envio barra quem "já respondeu") e a
    /// solicitação voltar para "Não confirmados" em Confirmações. O "não vou" só é reaberto por uma
    /// pessoa (<paramref name="incluindoNaoVou"/>).
    /// </summary>
    private static void ReabrirConfirmacao(Solicitacao s, DateTime agora, bool incluindoNaoVou)
    {
        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente) return;
        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada && !incluindoNaoVou) return;

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente;
        s.ConfirmadoEm = null;
        s.ConfirmadoCanal = null;
        s.ConfirmacaoCanceladaEm = null;
        s.MotivoCancelamentoPaciente = null;
        s.AtualizadoEm = agora;
    }

    /// <summary>"A confirmação de 01/11 08:12 (pelo link)" — para a nota dizer o que deixou de valer.</summary>
    private static string RespostaAnterior(Solicitacao s) => s.StatusConfirmacao switch
    {
        StatusConfirmacaoAgendamento.Confirmada =>
            "A confirmação" + (s.ConfirmadoEm is { } em ? $" de {Quando(em)}" : string.Empty)
            + (string.IsNullOrWhiteSpace(s.ConfirmadoCanal) ? string.Empty : $" ({s.ConfirmadoCanal})"),
        StatusConfirmacaoAgendamento.Cancelada =>
            "O \"não vou\" do paciente"
            + (string.IsNullOrWhiteSpace(s.MotivoCancelamentoPaciente) ? string.Empty : $" (\"{s.MotivoCancelamentoPaciente}\")"),
        _ => "A resposta anterior",
    };

    private static string Quando(DateTime utc) => FusoBrasilia.ParaExibicao(utc).ToString("dd/MM HH:mm");

    /// <summary>
    /// O lembrete era da data antiga. O que ainda ia sair, sai na hora errada (X dias antes da data
    /// velha); o que já saiu falou da data velha. Dispensado com o carimbo, ele volta a valer para a
    /// data nova — o <see cref="LembreteAgendamentoService"/> o rearma quando chegar a hora.
    /// </summary>
    private async Task DispensarLembreteAsync(Guid solicitacaoId, DateTime agora, CancellationToken ct)
    {
        var lembrete = await db.ComunicacoesPaciente.FirstOrDefaultAsync(c => c.SolicitacaoId == solicitacaoId
            && c.Finalidade == FinalidadeComunicacao.LembreteAgendamento, ct);
        if (lembrete is null
            || (lembrete.Status == StatusComunicacao.Dispensada
                && lembrete.MotivoFalha?.StartsWith(CarimboLembreteRemarcado, StringComparison.Ordinal) == true))
            return;

        lembrete.Status = StatusComunicacao.Dispensada;
        lembrete.MotivoFalha = lembrete.EnviadoEm is { } enviadoEm
            ? $"{CarimboLembreteRemarcado}: este lembrete falou da data antiga (saiu em {Quando(enviadoEm)}); volta a valer para a nova."
            : $"{CarimboLembreteRemarcado}: o lembrete volta a valer para a data nova.";
        lembrete.ProximaTentativaEm = null;
        lembrete.AtualizadoEm = agora;
    }
}

/// <summary>
/// Olha as remarcações do SISREG de tempos em tempos. A sincronização roda de madrugada e o envio só
/// abre de manhã: 10 min põe tudo na fila muito antes de a primeira mensagem poder sair.
/// </summary>
public sealed class AvisoRemarcacaoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AvisoRemarcacaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IAvisoRemarcacaoService>()
                    .ProcessarPendentesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Nunca deixa subir: BackgroundServiceExceptionBehavior=StopHost derrubaria a API.
                logger.LogError(ex, "Falha ao avisar as remarcações do SISREG — tenta de novo.");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
