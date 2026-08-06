using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Credenciais;
using SMSMarica.Core.Integracoes.Credenciais.Dtos;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Integracoes.SerWeb.Varredura.Background;
using SMSMarica.Core.Ser.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser;

/// <summary>
/// Operação do motor do SER: estado, histórico de rodadas e disparo — a aba SER da
/// Configuração da Regulação.
/// </summary>
public interface ISerMotorService
{
    Task<SerStatusMotorDto> ObterStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SerExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken cancellationToken);

    /// <summary>Enfileira uma rodada. Recusa (409 via <see cref="ConflitoException"/>) se já
    /// houver uma em andamento — a sessão do SER é única por operador.</summary>
    Task<Guid?> DispararAsync(
        SerDispararVarreduraDto pedido, Guid? usuarioId, string? usuarioNome,
        CancellationToken cancellationToken);

    /// <summary>Testa uma credencial avulsa contra o SER sem gravá-la.</summary>
    Task TestarCredencialAsync(string usuario, string senha, CancellationToken cancellationToken);

    /// <summary>
    /// Grava a credencial do SER (cifrada) no store de integrações.
    ///
    /// <para><b>Só grava depois que o SER aceitou.</b> Autentica antes de persistir — mesma régua
    /// da credencial do SISREG por unidade. Salvar uma credencial que não funciona deixaria o
    /// motor falhando de madrugada, sem ninguém por perto para entender o porquê.</para>
    /// </summary>
    Task SalvarCredencialAsync(string usuario, string senha, CancellationToken cancellationToken);
}

public sealed class SerMotorService(
    SmsMaricaDbContext db,
    IVarreduraSerFila fila,
    ISerWebSessao sessao,
    IIntegracaoCredencialService credenciais) : ISerMotorService
{
    /// <summary>A solicitação mais antiga vista em produção é de 2016; começamos antes disso
    /// para a carga inicial não deixar cauda para trás.</summary>
    private static readonly DateOnly InicioPadrao = new(2015, 1, 1);

    public async Task<SerStatusMotorDto> ObterStatusAsync(CancellationToken cancellationToken)
    {
        var credencialOk = await CredencialConfiguradaAsync(cancellationToken);

        var ultima = await db.SerVarreduraExecucoes
            .AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        var emAndamento = fila.TemTrabalho
            || await db.SerVarreduraExecucoes.AnyAsync(
                x => x.Status == StatusVarreduraSer.EmExecucao || x.Status == StatusVarreduraSer.Pendente,
                cancellationToken);

        return new SerStatusMotorDto(
            credencialOk,
            emAndamento,
            await db.SerSolicitacoes.CountAsync(x => x.ExcluidoEm == null, cancellationToken),
            await db.SerEventos.CountAsync(cancellationToken),
            await db.SerGatilhos.CountAsync(x => x.ProcessadoEm == null, cancellationToken),
            ultima is null ? null : ParaDto(ultima));
    }

    public async Task<IReadOnlyList<SerExecucaoDto>> ListarExecucoesAsync(
        int limite, CancellationToken cancellationToken) =>
        await db.SerVarreduraExecucoes
            .AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => ParaDto(x))
            .ToListAsync(cancellationToken);

    public async Task<Guid?> DispararAsync(
        SerDispararVarreduraDto pedido, Guid? usuarioId, string? usuarioNome,
        CancellationToken cancellationToken)
    {
        if (!await CredencialConfiguradaAsync(cancellationToken))
        {
            throw new ValidacaoException(
                "ser.credencial_incompleta",
                "Configure o usuário e a senha do SER antes de disparar a varredura.");
        }

        var inicio = pedido.Inicio ?? InicioPadrao;
        var fim = pedido.Fim ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        if (inicio > fim)
        {
            throw new ValidacaoException(
                "ser.janela_invalida", "A data inicial da janela é posterior à final.");
        }

        var enfileirou = fila.TentarEnfileirar(new PedidoVarreduraSer(
            pedido.Modo, DisparoSincronizacao.Manual, inicio, fim, pedido.Situacoes,
            usuarioId, usuarioNome));

        if (!enfileirou)
        {
            // Recusar é melhor que enfileirar: a sessão do SER é única por operador e a segunda
            // rodada derrubaria a primeira no meio, deixando cobertura incompleta sem aviso.
            throw new ConflitoException(
                "ser.varredura_em_andamento",
                "Já existe uma varredura do SER em andamento. Aguarde ela terminar.");
        }

        return null; // a execução é criada pelo runner; a tela acompanha por ObterStatusAsync.
    }

    public async Task TestarCredencialAsync(
        string usuario, string senha, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException("ser.credencial_incompleta", "Informe usuário e senha.");
        }

        // Só autentica e confirma que o módulo Ambulatório abre. Nada é gravado.
        await sessao.AutenticarAvulsoAsync(usuario, senha, cancellationToken);
    }

    public async Task SalvarCredencialAsync(
        string usuario, string senha, CancellationToken cancellationToken)
    {
        // Valida ANTES de persistir: credencial que não funciona faria o motor falhar de
        // madrugada, sem ninguém por perto para entender o porquê.
        await TestarCredencialAsync(usuario, senha, cancellationToken);

        await credenciais.AtualizarAsync(
            SerWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: usuario,
                ClientSecret: senha,
                RedirectUri: null,
                ParametrosJson: null,
                Ativo: true),
            cancellationToken);

        // A sessão em memória guarda a credencial antiga; sem isto o motor só usaria a nova
        // depois de reiniciar o serviço.
        sessao.Reiniciar();
    }

    private async Task<bool> CredencialConfiguradaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(SerWebSessao.Provedor, cancellationToken);
            return !string.IsNullOrWhiteSpace(ctx.ClientId) && !string.IsNullOrWhiteSpace(ctx.ClientSecret);
        }
        catch (ValidacaoException)
        {
            // Provedor não cadastrado/inativo — para a tela isso é simplesmente "não configurado".
            return false;
        }
    }

    private static SerExecucaoDto ParaDto(SerVarreduraExecucao x) => new(
        x.Id, x.Modo, x.Disparo, x.Status, x.JanelaInicio, x.JanelaFim, x.SituacoesVarridas,
        x.Buscas, x.Paginas, x.SolicitacoesEncontradas, x.SolicitacoesNovas, x.SolicitacoesAtualizadas,
        x.MudancasSituacao, x.HistoricosLidos, x.EventosNovos, x.FollowUpsNovos,
        x.HistoricosIndisponiveis, x.GatilhosGerados, x.FatiasTruncadas,
        x.MensagemErro, x.IniciadoEm, x.FinalizadoEm, x.DuracaoSegundos, x.CriadoPorNome,
        x.Fase, x.CursorSituacao, x.CursorData, x.CursorIdSer, x.HistoricosPendentes,
        x.Retomadas, x.RetomadaEm);
}
