using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura.Background;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit;

/// <summary>Operação do motor do SERNIT: estado, histórico de rodadas, disparo e credencial — a aba
/// SERNIT da Configuração da Regulação.</summary>
public interface ISernitMotorService
{
    Task<SernitStatusMotorDto> ObterStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SernitExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken cancellationToken);

    /// <summary>Enfileira uma rodada. Recusa (409) se já houver uma em andamento (sessão única).</summary>
    Task<Guid?> DispararAsync(
        SernitDispararVarreduraDto pedido, Guid? usuarioId, string? usuarioNome,
        CancellationToken cancellationToken);

    Task TestarCredencialAsync(string usuario, string senha, CancellationToken cancellationToken);

    /// <summary>Grava a credencial do SERNIT (cifrada) — só depois que o SERNIT aceitou.</summary>
    Task SalvarCredencialAsync(string usuario, string senha, CancellationToken cancellationToken);
}

public sealed class SernitMotorService(
    SmsMaisDbContext db,
    IVarreduraSernitFila fila,
    ISernitWebSessao sessao,
    IIntegracaoCredencialService credenciais) : ISernitMotorService
{
    private static readonly DateOnly InicioPadrao = new(2015, 1, 1);

    public async Task<SernitStatusMotorDto> ObterStatusAsync(CancellationToken cancellationToken)
    {
        var credencialOk = await CredencialConfiguradaAsync(cancellationToken);

        var ultima = await db.SernitVarreduraExecucoes
            .AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        var emAndamento = fila.TemTrabalho
            || await db.SernitVarreduraExecucoes.AnyAsync(
                x => x.Status == StatusVarreduraSernit.EmExecucao || x.Status == StatusVarreduraSernit.Pendente,
                cancellationToken);

        return new SernitStatusMotorDto(
            credencialOk,
            emAndamento,
            await db.SernitSolicitacoes.CountAsync(x => x.ExcluidoEm == null, cancellationToken),
            await db.SernitEventos.CountAsync(cancellationToken),
            await db.SernitGatilhos.CountAsync(x => x.ProcessadoEm == null, cancellationToken),
            ultima is null ? null : ParaDto(ultima));
    }

    public async Task<IReadOnlyList<SernitExecucaoDto>> ListarExecucoesAsync(
        int limite, CancellationToken cancellationToken) =>
        await db.SernitVarreduraExecucoes
            .AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => ParaDto(x))
            .ToListAsync(cancellationToken);

    public async Task<Guid?> DispararAsync(
        SernitDispararVarreduraDto pedido, Guid? usuarioId, string? usuarioNome,
        CancellationToken cancellationToken)
    {
        if (!await CredencialConfiguradaAsync(cancellationToken))
        {
            throw new ValidacaoException(
                "sernit.credencial_incompleta",
                "Configure o usuário e a senha do SERNIT antes de disparar a varredura.");
        }

        var inicio = pedido.Inicio ?? InicioPadrao;
        var fim = pedido.Fim ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        if (inicio > fim)
        {
            throw new ValidacaoException("sernit.janela_invalida", "A data inicial da janela é posterior à final.");
        }

        var enfileirou = fila.TentarEnfileirar(new PedidoVarreduraSernit(
            pedido.Modo, DisparoSincronizacao.Manual, inicio, fim, pedido.Situacoes,
            usuarioId, usuarioNome));

        if (!enfileirou)
        {
            throw new ConflitoException(
                "sernit.varredura_em_andamento",
                "Já existe uma varredura do SERNIT em andamento. Aguarde ela terminar.");
        }

        return null;
    }

    public async Task TestarCredencialAsync(string usuario, string senha, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException("sernit.credencial_incompleta", "Informe usuário e senha.");
        }

        await sessao.AutenticarAvulsoAsync(usuario, senha, cancellationToken);
    }

    public async Task SalvarCredencialAsync(string usuario, string senha, CancellationToken cancellationToken)
    {
        await TestarCredencialAsync(usuario, senha, cancellationToken);

        await credenciais.AtualizarAsync(
            SernitWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: usuario,
                ClientSecret: senha,
                RedirectUri: null,
                ParametrosJson: null,
                Ativo: true),
            cancellationToken);

        sessao.Reiniciar();
    }

    private async Task<bool> CredencialConfiguradaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(SernitWebSessao.Provedor, cancellationToken);
            return !string.IsNullOrWhiteSpace(ctx.ClientId) && !string.IsNullOrWhiteSpace(ctx.ClientSecret);
        }
        catch (ValidacaoException)
        {
            return false;
        }
    }

    private static SernitExecucaoDto ParaDto(SernitVarreduraExecucao x) => new(
        x.Id, x.Modo, x.Disparo, x.Status, x.JanelaInicio, x.JanelaFim, x.SituacoesVarridas,
        x.Buscas, x.Paginas, x.SolicitacoesEncontradas, x.SolicitacoesNovas, x.SolicitacoesAtualizadas,
        x.MudancasSituacao, x.HistoricosLidos, x.EventosNovos, x.FollowUpsNovos,
        x.HistoricosIndisponiveis, x.GatilhosGerados, x.FatiasTruncadas,
        x.MensagemErro, x.IniciadoEm, x.FinalizadoEm, x.DuracaoSegundos, x.CriadoPorNome,
        x.Fase, x.CursorSituacao, x.CursorData, x.CursorIdSernit, x.HistoricosPendentes,
        x.Retomadas, x.RetomadaEm, x.UltimoSinalEm);
}
