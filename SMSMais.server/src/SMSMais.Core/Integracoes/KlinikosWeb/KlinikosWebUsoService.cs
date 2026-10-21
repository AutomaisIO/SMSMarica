using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>Como a importação usa uma fonte Klinikos Web (ADR-0076: o uso fica no recurso).</summary>
/// <param name="WebPrimaria">Esta base é a dona da escrita no hub (só o Conde; as UPAs têm o SQL).</param>
/// <param name="PeriodicoIntervaloMin">Minutos entre puxadas periódicas; 0 = desligado. MANUAL e por
///   base — o motor nunca ajusta (o Crystal da origem vaza memória).</param>
public sealed record KlinikosWebUsoDto(
    Guid FonteId, string Nome, string? Slug, bool Ativa,
    bool WebPrimaria, bool PeriodicoLigado, int PeriodicoIntervaloMin, int DeepThrottleSeg);

public sealed record SalvarKlinikosWebUsoRequest(
    bool WebPrimaria, bool PeriodicoLigado, int PeriodicoIntervaloMin, int DeepThrottleSeg);

public interface IKlinikosWebUsoService
{
    Task<IReadOnlyList<KlinikosWebUsoDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task SalvarAsync(Guid fonteId, SalvarKlinikosWebUsoRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cadência e escrita do conector web do Klinikos, editadas em Importar Prontuários. Moram no
/// <c>parametros_json</c> da fonte, ao lado da conexão (que se edita em Sistema → Fontes de dados);
/// cada tela só mexe nas suas chaves — aqui, <see cref="ChavesUso"/>.
/// </summary>
public sealed class KlinikosWebUsoService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IKlinikosWebUsoService
{
    public static readonly IReadOnlyList<string> ChavesUso =
        ["webPrimaria", "periodicoLigado", "periodicoIntervaloMin", "deepThrottleSeg"];

    public async Task<IReadOnlyList<KlinikosWebUsoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var fontes = await db.IaFontes.AsNoTracking()
            .Where(f => f.ExcluidoEm == null && f.Tipo == TipoFonte.KlinikosWeb)
            .OrderBy(f => f.Nome)
            .ToListAsync(cancellationToken);

        return [.. fontes.Select(f =>
        {
            var v = KlinikosWebParametros.Resolver(f.Slug ?? string.Empty, f.ParametrosJson);
            return new KlinikosWebUsoDto(f.Id, f.Nome, f.Slug, f.Ativo,
                v.WebPrimaria, v.PeriodicoLigado, v.PeriodicoIntervaloMin, v.DeepThrottleSeg);
        })];
    }

    public async Task SalvarAsync(Guid fonteId, SalvarKlinikosWebUsoRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PeriodicoIntervaloMin < 0 || request.DeepThrottleSeg < 0)
        {
            throw new ValidacaoException("klinikosweb.uso_invalido", "Intervalo e espaçamento não podem ser negativos.");
        }

        var fonte = await db.IaFontes
            .FirstOrDefaultAsync(f => f.Id == fonteId && f.ExcluidoEm == null && f.Tipo == TipoFonte.KlinikosWeb, cancellationToken)
            ?? throw new NaoEncontradoException("FonteKlinikosWeb", fonteId);

        JsonObject obj;
        try
        {
            obj = string.IsNullOrWhiteSpace(fonte.ParametrosJson)
                ? new JsonObject()
                : JsonNode.Parse(fonte.ParametrosJson) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ValidacaoException("klinikosweb.parametros_corrompidos",
                "Os parâmetros desta fonte estão corrompidos — corrija a conexão em Sistema → Fontes de dados antes.");
        }

        obj["webPrimaria"] = request.WebPrimaria;
        obj["periodicoLigado"] = request.PeriodicoLigado;
        obj["periodicoIntervaloMin"] = request.PeriodicoIntervaloMin;
        obj["deepThrottleSeg"] = request.DeepThrottleSeg;

        fonte.ParametrosJson = obj.ToJsonString();
        fonte.AtualizadoEm = DateTime.UtcNow;
        fonte.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }
}
