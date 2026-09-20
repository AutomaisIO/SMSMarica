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

        c.HoraInicioEnvio = inicio;
        c.HoraFimEnvio = fim;
        c.MaximoPorPassagem = request.MaximoPorPassagem;
        c.SomenteSisreg = request.SomenteSisreg;
        c.LembreteDiasAntes = request.LembreteDiasAntes;
        c.LembreteHabilitado = request.LembreteHabilitado;
        c.AtualizadoEm = agora;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Mapear(c);
    }

    private static ConfirmacaoConfiguracaoDto Mapear(ConfirmacaoConfiguracao c) => new(
        c.HoraInicioEnvio.ToString("HH:mm"),
        c.HoraFimEnvio.ToString("HH:mm"),
        c.MaximoPorPassagem,
        c.SomenteSisreg,
        JanelaEnvioConfirmacao.Dentro(DateTime.UtcNow, c.HoraInicioEnvio, c.HoraFimEnvio),
        c.AtualizadoEm,
        c.LembreteDiasAntes,
        c.LembreteHabilitado);
}
