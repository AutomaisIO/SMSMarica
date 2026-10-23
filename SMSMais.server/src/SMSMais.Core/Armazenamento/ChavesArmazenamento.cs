using System.Globalization;

namespace SMSMais.Core.Armazenamento;

/// <summary>
/// Monta as chaves canônicas de armazenamento (separador <c>/</c>, compatível com S3).
/// Pasta = UUID do paciente; arquivo = UUID do documento.
/// </summary>
internal static class ChavesArmazenamento
{
    /// <summary><c>{prefixo}/{pacienteId}/{documentoId}.{extensao}</c>. Prefixo vazio é omitido.</summary>
    public static string Documento(string? prefixo, Guid pacienteId, Guid documentoId, string extensao = "pdf")
    {
        var ext = (extensao ?? "pdf").Trim('.', ' ');
        if (string.IsNullOrEmpty(ext)) ext = "pdf";
        var p = string.IsNullOrWhiteSpace(prefixo) ? string.Empty : prefixo.Trim('/') + "/";
        return string.Create(CultureInfo.InvariantCulture, $"{p}{pacienteId:D}/{documentoId:D}.{ext}");
    }

    /// <summary><c>{prefixo}/comunicacao-interna/{conversaId}/{mensagemId}.{extensao}</c>; sem extensão, sem ponto.</summary>
    public static string ComunicacaoInterna(string? prefixo, Guid conversaId, Guid mensagemId, string? extensao)
    {
        var ext = (extensao ?? string.Empty).Trim('.', ' ');
        var p = string.IsNullOrWhiteSpace(prefixo) ? string.Empty : prefixo.Trim('/') + "/";
        var sufixo = string.IsNullOrEmpty(ext) ? string.Empty : "." + ext;
        return string.Create(CultureInfo.InvariantCulture, $"{p}comunicacao-interna/{conversaId:D}/{mensagemId:D}{sufixo}");
    }
}
