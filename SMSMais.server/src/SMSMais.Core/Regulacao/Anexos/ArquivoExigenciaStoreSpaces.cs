using System.Globalization;

using SMSMais.Core.Armazenamento;

namespace SMSMais.Core.Regulacao.Anexos;

/// <summary>
/// Guarda os anexos da regulação no DigitalOcean Spaces, reaproveitando
/// <see cref="IArmazenamentoArquivos"/>.
///
/// <para><b>Spaces e não <c>midia</c> (bytea)</b> — decisão do Bernardo em 05/09/2026. O caminho
/// real destes anexos é foto de celular tirada no balcão, não PDF pequeno digitalizado: guardar
/// isso em coluna do Postgres infla backup e pool de conexão para tráfego que é de objeto.</para>
///
/// <para>Sem fallback local, pelo mesmo motivo do armazenamento de exames: um fallback que
/// "salva em algum lugar" produz registro órfão que ninguém encontra depois. Spaces fora do ar
/// ⇒ a operação falha e o operador vê.</para>
/// </summary>
public sealed class ArquivoExigenciaStoreSpaces(IArmazenamentoArquivos armazenamento)
    : IArquivoExigenciaStore
{
    private const string Prefixo = "Regulacao";

    public string MontarChave(Guid pacienteId, Guid arquivoId, string extensao)
    {
        var ext = (extensao ?? string.Empty).Trim('.', ' ').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) ext = "bin";
        return string.Create(CultureInfo.InvariantCulture, $"{Prefixo}/{pacienteId:D}/{arquivoId:D}.{ext}");
    }

    public Task SalvarAsync(string chave, byte[] conteudo, CancellationToken ct) =>
        armazenamento.SalvarAsync(chave, conteudo, ct);

    public Task<byte[]?> LerAsync(string chave, CancellationToken ct) =>
        armazenamento.LerAsync(chave, ct);

    public Task ExcluirAsync(string chave, CancellationToken ct) =>
        armazenamento.ExcluirAsync(chave, ct);
}
