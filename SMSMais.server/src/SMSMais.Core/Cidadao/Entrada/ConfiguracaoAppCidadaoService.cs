using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Auditoria.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Cidadao.Entrada;

public sealed class ConfiguracaoAppCidadaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IAuditoriaService auditoria,
    IIntegracaoCredencialService credenciais) : IConfiguracaoAppCidadaoService
{
    /// <summary>Entidade da trilha de auditoria — é por ela que a tela lista os ajustes.</summary>
    public const string EntidadeAuditoria = "ConfiguracaoAppCidadao";
    private const string IdAuditoria = "entrada";
    private const int AjustesNaTela = 50;

    public async Task<ConfiguracaoAppCidadaoDto> ObterAsync(CancellationToken ct = default)
    {
        await GarantirAdminGlobalAsync(ct);
        return await MontarAsync(await LerAsync(ct), ct);
    }

    public async Task<ConfiguracaoAppCidadaoDto> AtualizarAsync(
        AtualizarConfiguracaoAppCidadaoRequest request, CancellationToken ct = default)
    {
        await GarantirAdminGlobalAsync(ct);

        var atual = await db.ConfiguracoesAppCidadao.FirstOrDefaultAsync(c => c.Id == ConfiguracaoAppCidadao.IdSingleton, ct);
        var antes = Valores.De(atual);
        var depois = new Valores(
            request.Pwa?.ConsultarEsus ?? antes.PwaConsultarEsus,
            request.Nativo?.ConsultarEsus ?? antes.NativoConsultarEsus);

        if (depois != antes)
        {
            if (atual is null)
            {
                atual = new ConfiguracaoAppCidadao();
                db.ConfiguracoesAppCidadao.Add(atual);
            }

            atual.PwaConsultarEsus = depois.PwaConsultarEsus;
            atual.NativoConsultarEsus = depois.NativoConsultarEsus;
            atual.AtualizadoEm = DateTime.UtcNow;
            atual.AtualizadoPor = usuarioAtual.UsuarioId;
            await db.SaveChangesAsync(ct);

            await auditoria.RegistrarAsync(
                EntidadeAuditoria, IdAuditoria, "Alterou",
                JsonSerializer.Serialize(antes, Json), JsonSerializer.Serialize(depois, Json), ct);
        }

        return await MontarAsync(atual, ct);
    }

    public async Task<bool> ConsultarEsusNoCanalAsync(string canal, CancellationToken ct = default)
    {
        var c = await LerAsync(ct);
        return CanalAppCidadao.Normalizar(canal) == CanalAppCidadao.Nativo
            ? c?.NativoConsultarEsus ?? false
            : c?.PwaConsultarEsus ?? false;
    }

    private Task<ConfiguracaoAppCidadao?> LerAsync(CancellationToken ct) =>
        db.ConfiguracoesAppCidadao.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ConfiguracaoAppCidadao.IdSingleton, ct);

    private async Task<ConfiguracaoAppCidadaoDto> MontarAsync(ConfiguracaoAppCidadao? c, CancellationToken ct)
    {
        var v = Valores.De(c);
        var esus = await credenciais.ObterAsync(CorrecaoTelefoneEsusService.Provedor, ct);
        var quem = c?.AtualizadoPor is { } id
            ? await db.Usuarios.AsNoTracking().Where(u => u.Id == id).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct)
            : null;

        var trilha = await auditoria.BuscarAsync(
            new AuditoriaFiltroDto(Entidade: EntidadeAuditoria, EntidadeId: IdAuditoria, Tamanho: AjustesNaTela), ct);
        var ajustes = trilha.Itens
            .Select(r => new AjusteConfiguracaoAppCidadaoDto(r.CriadoEm, r.UsuarioNome, Mudancas(r.ValorAnterior, r.ValorNovo)))
            .ToList();

        return new ConfiguracaoAppCidadaoDto(
            new CanalEntradaDto(v.PwaConsultarEsus),
            new CanalEntradaDto(v.NativoConsultarEsus),
            esus.ClientIdDefinido && esus.ClientSecretDefinido && esus.Ativo,
            c?.AtualizadoEm,
            quem,
            ajustes);
    }

    /// <summary>"PWA: consulta ao e-SUS desligada → ligada" — o que a tela mostra de cada ajuste.</summary>
    internal static IReadOnlyList<string> Mudancas(string? anterior, string? novo)
    {
        var a = Ler(anterior);
        var n = Ler(novo);
        var mudancas = new List<string>();
        if (a.PwaConsultarEsus != n.PwaConsultarEsus)
            mudancas.Add($"PWA: consulta ao e-SUS {Estado(a.PwaConsultarEsus)} → {Estado(n.PwaConsultarEsus)}");
        if (a.NativoConsultarEsus != n.NativoConsultarEsus)
            mudancas.Add($"App nativo: consulta ao e-SUS {Estado(a.NativoConsultarEsus)} → {Estado(n.NativoConsultarEsus)}");
        return mudancas;

        static string Estado(bool ligado) => ligado ? "ligada" : "desligada";
        static Valores Ler(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Valores.Padrao;
            try { return JsonSerializer.Deserialize<Valores>(json, Json) ?? Valores.Padrao; }
            catch (JsonException) { return Valores.Padrao; }
        }
    }

    private async Task GarantirAdminGlobalAsync(CancellationToken ct)
    {
        if (!await AcessoGlobalUsuario.TemAsync(db, usuarioAtual.UsuarioId, ct))
            throw new UnauthorizedAccessException("A configuração do app do cidadão é só de administrador com acesso global.");
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Valores(bool PwaConsultarEsus, bool NativoConsultarEsus)
    {
        public static readonly Valores Padrao = new(false, false);

        public static Valores De(ConfiguracaoAppCidadao? c) =>
            c is null ? Padrao : new(c.PwaConsultarEsus, c.NativoConsultarEsus);
    }
}
