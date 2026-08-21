using System.Text.Json;

namespace Automais.Zap.Core.Meta;

/// <summary>
/// Leitura do envelope da Meta, o mínimo necessário para rotear: a lista de
/// <c>entry[].changes[]</c> com o <c>phone_number_id</c> de cada um.
///
/// O relay nunca desserializa mensagem, contato ou status — só navega até o metadado e
/// recorta. O conteúdo passa adiante como bytes.
/// </summary>
public sealed class PayloadMeta : IDisposable
{
    private readonly JsonDocument _doc;

    private PayloadMeta(JsonDocument doc, IReadOnlyList<EventoMeta> eventos)
    {
        _doc = doc;
        Eventos = eventos;
    }

    public IReadOnlyList<EventoMeta> Eventos { get; }

    /// <summary>Total de <c>changes</c> no payload — usado para saber se um recorte pegou tudo.</summary>
    public int TotalEventos => Eventos.Count;

    public static bool TentarLer(ReadOnlySpan<byte> corpo, out PayloadMeta? payload, out string? erro)
    {
        payload = null;
        erro = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(corpo.ToArray());
        }
        catch (JsonException ex)
        {
            erro = "JSON inválido: " + ex.Message;
            return false;
        }

        try
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                erro = "Raiz do payload não é um objeto.";
                doc.Dispose();
                return false;
            }

            var eventos = new List<EventoMeta>();

            if (doc.RootElement.TryGetProperty("entry", out var entries)
                && entries.ValueKind == JsonValueKind.Array)
            {
                var ie = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    var wabaId = entry.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                        ? idEl.GetString()
                        : null;

                    if (entry.TryGetProperty("changes", out var changes)
                        && changes.ValueKind == JsonValueKind.Array)
                    {
                        var ic = 0;
                        foreach (var change in changes.EnumerateArray())
                        {
                            string? phoneNumberId = null;
                            if (change.TryGetProperty("value", out var value)
                                && value.ValueKind == JsonValueKind.Object
                                && value.TryGetProperty("metadata", out var metadata)
                                && metadata.ValueKind == JsonValueKind.Object
                                && metadata.TryGetProperty("phone_number_id", out var pni)
                                && pni.ValueKind == JsonValueKind.String)
                            {
                                phoneNumberId = pni.GetString();
                            }

                            var field = change.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String
                                ? f.GetString() ?? "desconhecido"
                                : "desconhecido";

                            eventos.Add(new EventoMeta(ie, ic, phoneNumberId, wabaId, field));
                            ic++;
                        }
                    }

                    ie++;
                }
            }

            payload = new PayloadMeta(doc, eventos);
            return true;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reconstrói o payload contendo APENAS os changes das coordenadas pedidas, preservando
    /// todo o resto do envelope.
    ///
    /// É isto que impede vazamento entre clientes: um POST da Meta pode trazer eventos de mais
    /// de um WABA, e entregar o corpo inteiro a cada destino faria o município A receber
    /// mensagem do município B. Como o corpo muda, quem chama tem de reassinar — e sob o App
    /// único o App Secret é o mesmo dos dois lados, então a instância valida sem saber que
    /// houve recorte.
    /// </summary>
    public byte[] Fatiar(IReadOnlySet<(int Entry, int Change)> coordenadas)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();

            foreach (var prop in _doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("entry")) continue;
                prop.WriteTo(w);
            }

            w.WritePropertyName("entry");
            w.WriteStartArray();

            if (_doc.RootElement.TryGetProperty("entry", out var entries)
                && entries.ValueKind == JsonValueKind.Array)
            {
                var ie = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    var indiceEntry = ie++;
                    if (!coordenadas.Any(c => c.Entry == indiceEntry)) continue;

                    w.WriteStartObject();

                    foreach (var prop in entry.EnumerateObject())
                    {
                        if (prop.NameEquals("changes")) continue;
                        prop.WriteTo(w);
                    }

                    w.WritePropertyName("changes");
                    w.WriteStartArray();

                    if (entry.TryGetProperty("changes", out var changes)
                        && changes.ValueKind == JsonValueKind.Array)
                    {
                        var ic = 0;
                        foreach (var change in changes.EnumerateArray())
                        {
                            var indiceChange = ic++;
                            if (!coordenadas.Contains((indiceEntry, indiceChange))) continue;
                            change.WriteTo(w);
                        }
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return buffer.ToArray();
    }

    public void Dispose() => _doc.Dispose();
}
