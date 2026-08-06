using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Fhir;

/// <summary>
/// Regras comuns de ESCRITA que todo service aplica antes de gravar uma linha.
/// </summary>
public static class EscritaFhir
{
    /// <summary>
    /// O recurso que chegou é igual ao que já está guardado? Se for, o PUT não deve virar uma
    /// versão nova.
    ///
    /// <para><b>Por que existe.</b> Quem alimenta o hub são conectores de PEP que releem um
    /// bloco fixo de registros a cada ciclo <b>de propósito</b>: internação em curso é re-lida
    /// todo poll para capturar transferência de leito e alta (ADR-0025), e o cadastro de médicos
    /// é re-scan integral. Sem esta guarda, cada poll gravava um PUT idêntico ao anterior —
    /// medido em produção 06/08/2026: pacientes internados no HMCML em <c>version_id</c> 226,
    /// ~150 pacientes e ~750 profissionais reescritos a cada 11 minutos. Nada corrompia, mas
    /// queimava escrita e tirava o sentido de <c>lastUpdated</c> para auditoria: tudo parecia
    /// ter mudado agora, sempre.</para>
    ///
    /// <para><b>O que fica de fora da comparação</b>: <c>meta.versionId</c> e
    /// <c>meta.lastUpdated</c>, que são justamente o que o hub carimba a cada escrita. Todo o
    /// resto participa, <c>meta.source</c> inclusive — recurso que passou a ser visto por outra
    /// base MUDOU.</para>
    ///
    /// <para>Comparar texto exigiria que a ordem das listas (identifiers, telecoms) fosse
    /// estável. É, em regime: o que está guardado é o recurso que o próprio conector montou no
    /// ciclo anterior. Quando não for, a guarda erra para o lado seguro — devolve
    /// <c>false</c> e escreve, que é o comportamento antigo.</para>
    /// </summary>
    /// <param name="novo">Recurso já carimbado (id/meta.source), pronto para serializar.</param>
    /// <param name="conteudoArmazenado">O <c>Content</c> da linha, como está no jsonb.</param>
    public static bool SemMudanca<T>(T novo, string conteudoArmazenado) where T : Resource
    {
        T armazenado;
        try
        {
            armazenado = FhirJson.Parse<T>(conteudoArmazenado);
        }
        catch (Exception)
        {
            // Conteúdo ilegível (legado de um schema anterior, gravação truncada): não é hora de
            // decidir isso aqui. Segue a escrita — que, de quebra, conserta a linha.
            return false;
        }

        return SemMetaVolatil(novo) == SemMetaVolatil(armazenado);
    }

    /// <summary>
    /// JSON do recurso sem os dois campos que mudam a cada escrita. Restaura os valores no
    /// <c>finally</c>: o <c>versionId</c> do recurso que chegou pode ser o If-Match de quem
    /// chamou, e zerá-lo em definitivo desligaria a concorrência otimista em silêncio.
    /// </summary>
    private static string SemMetaVolatil(Resource r)
    {
        var meta = r.Meta;
        var versao = meta?.VersionId;
        var atualizado = meta?.LastUpdated;
        try
        {
            if (meta is not null) { meta.VersionId = null; meta.LastUpdated = null; }
            return FhirJson.Serialize(r);
        }
        finally
        {
            if (meta is not null) { meta.VersionId = versao; meta.LastUpdated = atualizado; }
        }
    }
}
