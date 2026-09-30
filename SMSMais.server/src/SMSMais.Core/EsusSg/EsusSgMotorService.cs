using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.EsusSg.Dtos;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.EsusSgWeb.Varredura.Background;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Core.EsusSg;

/// <summary>Liga/desliga e horário da rodada diária do ESUS SG — mora no <c>ParametrosJson</c> da
/// credencial <c>esussg</c>, como no SER/SERNIT.</summary>
public interface IEsusSgVarreduraConfigService
{
    Task<EsusSgVarreduraConfigDto> ObterAsync(CancellationToken cancellationToken);
    Task<EsusSgVarreduraConfigDto> SalvarAsync(EsusSgVarreduraConfigDto config, CancellationToken cancellationToken);
}

public sealed class EsusSgVarreduraConfigService(IIntegracaoCredencialService credenciais)
    : IEsusSgVarreduraConfigService
{
    public const string ChaveAtivo = "varreduraAtiva";
    public const string ChaveHora = "varreduraHoraLocal";
    public const string HoraPadrao = "03:00";

    public async Task<EsusSgVarreduraConfigDto> ObterAsync(CancellationToken cancellationToken)
    {
        var json = await EsusSgParametros.LerAsync(credenciais, cancellationToken);
        return new EsusSgVarreduraConfigDto(
            json?[ChaveAtivo]?.GetValue<bool>() ?? false,
            json?[ChaveHora]?.GetValue<string>() ?? HoraPadrao);
    }

    public async Task<EsusSgVarreduraConfigDto> SalvarAsync(
        EsusSgVarreduraConfigDto config, CancellationToken cancellationToken)
    {
        if (!TimeOnly.TryParseExact(config.HoraLocal?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var t))
        {
            throw new ValidacaoException(
                "esussg.hora_invalida", $"Hora inválida: \"{config.HoraLocal}\". Use HH:mm (ex.: 03:00).");
        }
        var hora = t.ToString("HH:mm", CultureInfo.InvariantCulture);

        await EsusSgParametros.AlterarAsync(credenciais, json =>
        {
            json[ChaveAtivo] = config.Ativo;
            json[ChaveHora] = hora;
        }, cancellationToken);

        return new EsusSgVarreduraConfigDto(config.Ativo, hora);
    }
}

/// <summary>Leitura e escrita do <c>ParametrosJson</c> da credencial <c>esussg</c> sem perder as
/// chaves que outra tela gravou (cliente, URLs, agendamento, operadores da estatística).</summary>
internal static class EsusSgParametros
{
    public static async Task<JsonObject?> LerAsync(IIntegracaoCredencialService credenciais, CancellationToken ct)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(EsusSgSessao.Provedor, ct);
            return Parse(ctx.ParametrosJson);
        }
        catch (ValidacaoException)
        {
            return null;
        }
    }

    public static async Task AlterarAsync(
        IIntegracaoCredencialService credenciais, Action<JsonObject> alterar, CancellationToken ct,
        string? usuario = null, string? senha = null)
    {
        JsonObject json;
        string? redirect = null;
        var ativo = true;
        try
        {
            var atual = await credenciais.ObterAsync(EsusSgSessao.Provedor, ct);
            json = Parse(atual.ParametrosJson) ?? new JsonObject();
            redirect = atual.RedirectUri;
            ativo = atual.Ativo || usuario is not null;
        }
        catch (Exception ex) when (ex is ValidacaoException or NaoEncontradoException)
        {
            json = new JsonObject();
        }

        alterar(json);
        await credenciais.AtualizarAsync(
            EsusSgSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: usuario, ClientSecret: senha, RedirectUri: redirect,
                ParametrosJson: json.ToJsonString(), Ativo: ativo),
            ct);
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Fachada do motor do ESUS SG para a tela de Configuração (status, rodadas, disparo,
/// credencial). Espelho do <c>SernitMotorService</c>.</summary>
public interface IEsusSgMotorService
{
    Task<EsusSgStatusMotorDto> ObterStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EsusSgExecucaoDto>> ListarExecucoesAsync(int limite, CancellationToken cancellationToken);
    Task DispararAsync(EsusSgDispararVarreduraDto pedido, Guid? usuarioId, string? usuarioNome, CancellationToken cancellationToken);
    Task<string> TestarCredencialAsync(EsusSgCredencialRequest credencial, CancellationToken cancellationToken);
    Task SalvarCredencialAsync(EsusSgCredencialRequest credencial, CancellationToken cancellationToken);
}

public sealed class EsusSgMotorService(
    SmsMaisDbContext db,
    IVarreduraEsusSgFila fila,
    IEsusSgSessao sessao,
    IIntegracaoCredencialService credenciais) : IEsusSgMotorService
{
    public async Task<EsusSgStatusMotorDto> ObterStatusAsync(CancellationToken cancellationToken)
    {
        string? usuario = null;
        var cliente = EsusSgSessao.ClientePadrao;
        var credencialOk = false;
        try
        {
            var ctx = await credenciais.ObterContextoAsync(EsusSgSessao.Provedor, cancellationToken);
            credencialOk = !string.IsNullOrWhiteSpace(ctx.ClientId) && !string.IsNullOrWhiteSpace(ctx.ClientSecret);
            usuario = ctx.ClientId;
            cliente = ParametrosEsusSg.Ler(ctx.ParametrosJson).Cliente;
        }
        catch (ValidacaoException)
        {
            // Sem credencial ainda: o status mostra "Falta".
        }

        var ultima = await db.EsusSgVarreduraExecucoes.AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        var emAndamento = fila.TemTrabalho
            || await db.EsusSgVarreduraExecucoes.AnyAsync(
                x => x.Status == StatusVarreduraEsusSg.EmExecucao || x.Status == StatusVarreduraEsusSg.Pendente,
                cancellationToken);

        return new EsusSgStatusMotorDto(
            credencialOk,
            usuario,
            cliente,
            emAndamento,
            await db.EsusSgSolicitacoes.CountAsync(x => x.ExcluidoEm == null, cancellationToken),
            await db.EsusSgEventos.CountAsync(cancellationToken),
            await db.EsusSgGatilhos.CountAsync(x => x.ProcessadoEm == null, cancellationToken),
            await db.EsusSgCatalogoRecursos.CountAsync(x => x.Ativo, cancellationToken),
            ultima is null ? null : ParaDto(ultima));
    }

    public async Task<IReadOnlyList<EsusSgExecucaoDto>> ListarExecucoesAsync(
        int limite, CancellationToken cancellationToken) =>
        await db.EsusSgVarreduraExecucoes.AsNoTracking()
            .OrderByDescending(x => x.IniciadoEm)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => ParaDto(x))
            .ToListAsync(cancellationToken);

    public async Task DispararAsync(
        EsusSgDispararVarreduraDto pedido, Guid? usuarioId, string? usuarioNome, CancellationToken cancellationToken)
    {
        if (!await CredencialConfiguradaAsync(cancellationToken))
        {
            throw new ValidacaoException(
                "esussg.credencial_incompleta", "Configure o usuário e a senha do ESUS antes de disparar a varredura.");
        }

        var hoje = FusoBrasilia.HojeEmBrasilia();
        var (inicio, fim) = pedido.Modo == ModoVarreduraEsusSg.CargaInicial
            ? EsusSgJanelas.CargaInicial(hoje)
            : EsusSgJanelas.Diaria(hoje);

        if (!fila.TentarEnfileirar(new PedidoVarreduraEsusSg(
                pedido.Modo, DisparoSincronizacao.Manual, inicio, fim, usuarioId, usuarioNome)))
        {
            throw new ConflitoException(
                "esussg.varredura_em_andamento", "Já existe uma varredura do ESUS em andamento. Aguarde ela terminar.");
        }
    }

    public async Task<string> TestarCredencialAsync(EsusSgCredencialRequest credencial, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credencial.Usuario) || string.IsNullOrWhiteSpace(credencial.Senha))
        {
            throw new ValidacaoException("esussg.credencial_incompleta", "Informe usuário e senha.");
        }
        return await sessao.AutenticarAvulsoAsync(
            credencial.Usuario.Trim(), credencial.Senha, credencial.Cliente, cancellationToken);
    }

    public async Task SalvarCredencialAsync(EsusSgCredencialRequest credencial, CancellationToken cancellationToken)
    {
        await TestarCredencialAsync(credencial, cancellationToken);
        var cliente = string.IsNullOrWhiteSpace(credencial.Cliente)
            ? EsusSgSessao.ClientePadrao
            : credencial.Cliente.Trim().ToUpperInvariant();

        await EsusSgParametros.AlterarAsync(credenciais, json => json["cliente"] = cliente, cancellationToken,
            usuario: credencial.Usuario.Trim(), senha: credencial.Senha);

        sessao.Reiniciar();
    }

    private async Task<bool> CredencialConfiguradaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ctx = await credenciais.ObterContextoAsync(EsusSgSessao.Provedor, cancellationToken);
            return !string.IsNullOrWhiteSpace(ctx.ClientId) && !string.IsNullOrWhiteSpace(ctx.ClientSecret);
        }
        catch (ValidacaoException)
        {
            return false;
        }
    }

    private static EsusSgExecucaoDto ParaDto(EsusSgVarreduraExecucao x) => new(
        x.Id, x.Modo, x.Disparo, x.Status, x.JanelaInicio, x.JanelaFim,
        x.Requisicoes, x.NaFila, x.AgendadosLidos, x.SolicitacoesNovas, x.SolicitacoesAtualizadas,
        x.MudancasSituacao, x.SaidasDaFila, x.EventosNovos, x.GatilhosGerados, x.MesesIncompletos,
        x.MensagemErro, x.IniciadoEm, x.FinalizadoEm, x.DuracaoSegundos, x.CriadoPorNome,
        x.Fase, x.CursorMes, x.Retomadas, x.RetomadaEm, x.UltimoSinalEm);
}
