using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>Regras de disparo da confirmação de agendamento (menu Confirmações).</summary>
public interface IConfirmacaoConfiguracaoService
{
    /// <summary>Configuração vigente; sem linha no banco, os padrões (08–18h, 100/passagem, só SISREG).</summary>
    Task<ConfirmacaoConfiguracaoDto> ObterAsync(CancellationToken ct = default);

    Task<ConfirmacaoConfiguracaoDto> SalvarAsync(SalvarConfirmacaoConfiguracaoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Anota que o fechamento daquele dia foi concluído. Chamado pelo motor, nunca pela tela —
    /// é o que permite recuperar um dia perdido e não refazer um dia já feito depois de um restart.
    /// </summary>
    Task MarcarDiaFechadoAsync(DateOnly dia, CancellationToken ct = default);
}

public static class OrigemAgendamento
{
    /// <summary>A solicitação veio do SISREG? (importação, varredura ou extensão; acervo antigo sem
    /// proveniência conta pelo RAW do TXT.)</summary>
    public static bool EhDoSisreg(Solicitacao s) =>
        s.FonteCriacao is FonteSolicitacao.ImportacaoSisreg or FonteSolicitacao.ExtensaoNavegador
        || (s.FonteCriacao is null && s.RawSisreg is not null);
}

public sealed class ConfirmacaoConfiguracaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IConfirmacaoConfiguracaoService
{
    public const int MaximoPorPassagemTeto = 1000;

    public async Task<ConfirmacaoConfiguracaoDto> ObterAsync(CancellationToken ct = default)
    {
        var c = await db.ConfirmacaoConfiguracoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ConfirmacaoConfiguracao.IdSingleton, ct)
            ?? new ConfirmacaoConfiguracao();
        return Mapear(c);
    }

    public async Task<ConfirmacaoConfiguracaoDto> SalvarAsync(
        SalvarConfirmacaoConfiguracaoRequest request, CancellationToken ct = default)
    {
        if (!TimeOnly.TryParse(request.HoraInicioEnvio, out var inicio)
            || !TimeOnly.TryParse(request.HoraFimEnvio, out var fim))
            throw new ValidacaoException("confirmacao.horario_invalido", "Informe os horários no formato HH:mm.");
        if (inicio >= fim)
            throw new ValidacaoException("confirmacao.janela_invalida",
                "O horário de início precisa ser anterior ao de fim (ex.: 08:00 às 18:00).");
        if (request.MaximoPorPassagem is < 1 or > MaximoPorPassagemTeto)
            throw new ValidacaoException("confirmacao.vazao_invalida",
                $"A quantidade por rodada deve ficar entre 1 e {MaximoPorPassagemTeto}.");
        if (request.LembreteDiasAntes is < 1 or > 30)
            throw new ValidacaoException("confirmacao.lembrete_dias_invalido",
                "O lembrete deve sair de 1 a 30 dias antes do agendamento.");

        var agora = DateTime.UtcNow;
        var c = await db.ConfirmacaoConfiguracoes
            .FirstOrDefaultAsync(x => x.Id == ConfirmacaoConfiguracao.IdSingleton, ct);
        if (c is null)
        {
            c = new ConfirmacaoConfiguracao { CriadoEm = agora };
            db.ConfirmacaoConfiguracoes.Add(c);
        }

        // Campo ausente = manter o que está gravado. A validação tem de olhar o valor EFETIVO, não
        // o do pedido: quem manda só a hora de início precisa ser barrado contra a hora de fim que
        // já existe, senão a combinação inválida entra pela porta de quem omitiu metade dela.
        var intervalo = request.ConciliacaoIntervaloMinutos ?? c.ConciliacaoIntervaloMinutos;
        var horaInicio = request.ConciliacaoHoraInicio ?? c.ConciliacaoHoraInicio;
        var horaFim = request.ConciliacaoHoraFim ?? c.ConciliacaoHoraFim;
        var horaFechamento = request.ConciliacaoHoraFechamento ?? c.ConciliacaoHoraFechamento;

        // Abaixo de 1 minuto viraria martelo no SISREG; acima de 2 horas o "tempo real" deixa de
        // existir e a passada de fechamento passa a fazer todo o trabalho.
        if (intervalo is < 1 or > 120)
            throw new ValidacaoException("confirmacao.conciliacao_intervalo_invalido",
                "A leitura dos cancelamentos deve ocorrer a cada 1 a 120 minutos.");
        if (horaInicio is < 0 or > 23 || horaFim is < 1 or > 24 || horaFechamento is < 0 or > 23)
            throw new ValidacaoException("confirmacao.conciliacao_horario_invalido",
                "A leitura começa entre 0h e 23h, termina entre 1h e 24h, e o fechamento fica "
                + "entre 0h e 23h.");
        if (horaInicio >= horaFim)
            throw new ValidacaoException("confirmacao.conciliacao_janela_invalida",
                "A hora de início da leitura precisa ser anterior à de fim.");

        // Janela de 24h não deixa hora nenhuma livre para o fechamento — e como a regra seguinte
        // exige que ele caia fora, a tela inteira ficaria impossível de salvar, com o erro
        // apontando para o campo errado. Barra aqui, dizendo o que realmente precisa mudar.
        if (horaFim - horaInicio >= 24)
            throw new ValidacaoException("confirmacao.conciliacao_janela_dia_inteiro",
                "A janela de leitura não pode cobrir o dia inteiro: o fechamento precisa de uma "
                + "hora livre, fora dela, para reler o dia anterior.");

        // O fechamento relê o dia ANTERIOR. Dentro da janela ele competiria com a leitura do dia
        // corrente e gastaria requisição em cima de dado que a passada normal já traz.
        if (horaFechamento >= horaInicio && horaFechamento < horaFim)
            throw new ValidacaoException("confirmacao.conciliacao_fechamento_invalido",
                "O fechamento relê o dia anterior e precisa ficar FORA da janela de leitura "
                + $"(hoje, {horaInicio:00}:00 às {horaFim:00}:00).");

        c.HoraInicioEnvio = inicio;
        c.HoraFimEnvio = fim;
        c.MaximoPorPassagem = request.MaximoPorPassagem;
        c.SomenteSisreg = request.SomenteSisreg;
        c.LembreteDiasAntes = request.LembreteDiasAntes;
        c.LembreteHabilitado = request.LembreteHabilitado;
        c.ConciliacaoCancelamentoHabilitada = request.ConciliacaoCancelamentoHabilitada;
        c.AvisoCancelamentoHabilitado = request.AvisoCancelamentoHabilitado;
        c.ConciliacaoIntervaloMinutos = intervalo;
        c.ConciliacaoHoraInicio = horaInicio;
        c.ConciliacaoHoraFim = horaFim;
        c.ConciliacaoHoraFechamento = horaFechamento;
        c.AtualizadoEm = agora;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Mapear(c);
    }

    public async Task MarcarDiaFechadoAsync(DateOnly dia, CancellationToken ct = default)
    {
        var c = await db.ConfirmacaoConfiguracoes
            .FirstOrDefaultAsync(x => x.Id == ConfirmacaoConfiguracao.IdSingleton, ct);
        if (c is null)
        {
            c = new ConfirmacaoConfiguracao { CriadoEm = DateTime.UtcNow };
            db.ConfirmacaoConfiguracoes.Add(c);
        }

        // Só avança. Se duas passadas correrem fora de ordem, a mais antiga não desfaz a recente.
        if (c.ConciliacaoUltimoDiaFechado is { } ja && ja >= dia) return;

        c.ConciliacaoUltimoDiaFechado = dia;
        await db.SaveChangesAsync(ct);
    }

    private static ConfirmacaoConfiguracaoDto Mapear(ConfirmacaoConfiguracao c) => new(
        c.HoraInicioEnvio.ToString("HH:mm"),
        c.HoraFimEnvio.ToString("HH:mm"),
        c.MaximoPorPassagem,
        c.SomenteSisreg,
        JanelaEnvioConfirmacao.Dentro(DateTime.UtcNow, c.HoraInicioEnvio, c.HoraFimEnvio),
        c.AtualizadoEm,
        c.LembreteDiasAntes,
        c.LembreteHabilitado,
        c.ConciliacaoCancelamentoHabilitada,
        c.AvisoCancelamentoHabilitado,
        c.ConciliacaoIntervaloMinutos,
        c.ConciliacaoHoraInicio,
        c.ConciliacaoHoraFim,
        c.ConciliacaoHoraFechamento,
        c.ConciliacaoUltimoDiaFechado);
}
