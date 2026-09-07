using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Solicitacoes;

public sealed record RegulacaoEventoDto(
    Guid Id,
    TipoEventoRegulacao Tipo,
    StatusRegulacao? De,
    StatusRegulacao? Para,
    string? UsuarioNome,
    PapelEventoRegulacao Papel,
    JsonElement? Diff,
    JsonElement? Detalhe,
    DateTime CriadoEm);

public interface IRegulacaoEventoService
{
    /// <summary>
    /// Acrescenta um evento à trilha. <b>Não chama <c>SaveChanges</c></b>: o evento e a mudança
    /// que ele descreve têm de ir na mesma transação, senão sobra evento sem fato (ou pior, fato
    /// sem evento).
    /// </summary>
    Task RegistrarAsync(
        Guid solicitacaoId,
        TipoEventoRegulacao tipo,
        PapelEventoRegulacao papel,
        CancellationToken ct,
        StatusRegulacao? de = null,
        StatusRegulacao? para = null,
        object? diff = null,
        object? detalhe = null);

    Task<IReadOnlyList<RegulacaoEventoDto>> ListarAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>
    /// Diferença entre dois preenchimentos canônicos, chave a chave: <c>{campo: {de, para}}</c>.
    /// <c>null</c> quando nada mudou — evento de "ajuste" sem diff é ruído na linha do tempo.
    /// </summary>
    IReadOnlyDictionary<string, object?>? Diferenca(JsonElement antes, JsonElement depois);
}

/// <inheritdoc cref="IRegulacaoEventoService"/>
public sealed class RegulacaoEventoService(
    SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IRegulacaoEventoService
{
    public async Task RegistrarAsync(
        Guid solicitacaoId,
        TipoEventoRegulacao tipo,
        PapelEventoRegulacao papel,
        CancellationToken ct,
        StatusRegulacao? de = null,
        StatusRegulacao? para = null,
        object? diff = null,
        object? detalhe = null)
    {
        var usuarioId = usuarioAtual.UsuarioId;

        // O nome é copiado agora, e não resolvido na leitura: usuário é renomeado e desativado, e
        // a linha do tempo tem de continuar legível anos depois.
        var nome = usuarioId is null
            ? null
            : await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(ct);

        db.RegulacaoEventos.Add(new RegulacaoEvento
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacaoId,
            Tipo = tipo,
            StatusAnterior = de,
            StatusNovo = para,
            UsuarioId = usuarioId,
            UsuarioNome = nome,
            Papel = usuarioId is null ? PapelEventoRegulacao.Sistema : papel,
            UnidadeAtivaId = usuarioAtual.UnidadeAtivaId,
            Ip = Cortar(usuarioAtual.Ip, 64),
            SessaoId = Cortar(usuarioAtual.SessaoId, 64),
            DiffJson = diff is null ? null : JsonSerializer.Serialize(diff),
            DetalheJson = detalhe is null ? null : JsonSerializer.Serialize(detalhe),
            CriadoEm = DateTime.UtcNow,
        });
    }

    public async Task<IReadOnlyList<RegulacaoEventoDto>> ListarAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var eventos = await db.RegulacaoEventos.AsNoTracking()
            .Where(e => e.SolicitacaoId == solicitacaoId)
            .OrderBy(e => e.CriadoEm).ThenBy(e => e.Id)
            .ToListAsync(ct);

        return [.. eventos.Select(e => new RegulacaoEventoDto(
            e.Id, e.Tipo, e.StatusAnterior, e.StatusNovo, e.UsuarioNome, e.Papel,
            Ler(e.DiffJson), Ler(e.DetalheJson), e.CriadoEm))];
    }

    public IReadOnlyDictionary<string, object?>? Diferenca(JsonElement antes, JsonElement depois)
    {
        if (antes.ValueKind != JsonValueKind.Object || depois.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var mudou = new Dictionary<string, object?>();

        foreach (var campo in depois.EnumerateObject())
        {
            var valorNovo = Texto(campo.Value);
            var valorAntigo = antes.TryGetProperty(campo.Name, out var v) ? Texto(v) : null;
            if (valorAntigo != valorNovo)
            {
                mudou[campo.Name] = new { de = valorAntigo, para = valorNovo };
            }
        }

        // Campo que sumiu conta como mudança: é o caso do "trocar procedimento", em que respostas
        // do formulário antigo caem. Sem isto, a troca apareceria como se nada tivesse sido perdido.
        foreach (var campo in antes.EnumerateObject())
        {
            if (!depois.TryGetProperty(campo.Name, out _))
            {
                mudou[campo.Name] = new { de = Texto(campo.Value), para = (string?)null };
            }
        }

        return mudou.Count == 0 ? null : mudou;
    }

    private static string? Texto(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => e.GetString(),
        _ => e.GetRawText(),
    };

    private static JsonElement? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Cortar(string? v, int max) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Length <= max ? v : v[..max];
}
