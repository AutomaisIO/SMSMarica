using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacientes.Fhir;

/// <summary>Resumo do paciente resolvido do hub FHIR (para embutir em DTOs/PDF/worklist).</summary>
public sealed record PacienteResumo(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    Sexo Sexo,
    /// <summary>Número do contato verificado (marcador no telecom FHIR), se houver.</summary>
    string? TelefoneVerificado = null,
    /// <summary>CEP do endereço do paciente (só dígitos ou como veio do hub), se houver.</summary>
    string? Cep = null,
    /// <summary>Melhor número de contato (celular &gt; verificado &gt; principal), se houver.</summary>
    string? Celular = null,
    /// <summary>Logradouro do endereço do paciente, se houver.</summary>
    string? Logradouro = null);

/// <summary>
/// Resolve dados de paciente do hub FHIR por id. Os dependentes (Laudo,
/// Tratamento, SolicitacaoExame, Translado) guardam só o PacienteId; este
/// resolver busca nome/CPF/CNS quando precisam exibir.
/// </summary>
public interface IPacienteResolver
{
    Task<PacienteResumo?> ResolverAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, PacienteResumo>> ResolverManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Busca ids de pacientes no hub FHIR por um termo livre (nome por prefixo/contém e/ou
    /// CPF/CNS por prefixo). Para as barras de pesquisa (solicitações, laudos, consultas).
    /// <paramref name="limite"/> segue o "itens por página" da tela. Nunca lança: hub
    /// indisponível → conjunto vazio (o chamador cai no match local por accession/código).
    /// </summary>
    Task<IReadOnlySet<Guid>> BuscarIdsPorTermoAsync(string termo, int limite = 50, CancellationToken ct = default);
}

public sealed class PacienteResolver(IPacienteFhirClient fhir, ILogger<PacienteResolver> logger) : IPacienteResolver
{
    /// <summary>Máximo de ids por busca em lote (_id=...) — limita o tamanho da URL.</summary>
    private const int TamanhoLoteBusca = 100;

    public async Task<PacienteResumo?> ResolverAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;
        try
        {
            var patient = await fhir.ObterAsync(id, ct);
            if (patient is null) return null;

            var dto = PacienteFhirMapper.ParaDto(patient);
            return new PacienteResumo(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Cns, dto.DataNascimento, dto.Sexo,
                dto.TelefoneVerificado, dto.Endereco?.Cep,
                dto.TelefoneCelular ?? dto.TelefoneVerificado ?? dto.TelefonePrincipal, dto.Endereco?.Logradouro);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Hub FHIR indisponível/erro/lento NÃO pode derrubar a listagem que só quer
            // exibir o nome: degrada para "sem nome" (o chamador mostra fallback) em
            // vez de propagar e virar 500. Só a cancelação do CHAMADOR propaga — o
            // timeout do HttpClient também lança TaskCanceledException, mas com o ct
            // do chamador intacto, e é hub lento: degrada como qualquer outra falha.
            logger.LogWarning(ex, "Falha ao resolver paciente {PacienteId} no hub FHIR — seguindo sem nome.", id);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, PacienteResumo>> ResolverManyAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distintos = ids.Where(i => i != Guid.Empty).Distinct().ToArray();
        if (distintos.Length == 0) return new Dictionary<Guid, PacienteResumo>();

        // Indexador (não ToDictionary): se dois ids resolverem para o mesmo paciente
        // canônico (.Id), a chave duplicada sobrescreve em vez de lançar
        // ArgumentException — que viraria 500 na listagem.
        var mapa = new Dictionary<Guid, PacienteResumo>(distintos.Length);

        // 1 busca em lote por chunk (_id=a,b,c) em vez de 1 GET por paciente: o pior caso
        // da listagem de conversas passava de N chamadas paralelas ao hub para poucas.
        foreach (var chunk in distintos.Chunk(TamanhoLoteBusca))
        {
            try
            {
                var bundle = await fhir.BuscarPorIdsAsync(chunk, ct);
                foreach (var e in bundle?.Entry ?? [])
                {
                    if (e.Resource is not Patient patient) continue;
                    var dto = PacienteFhirMapper.ParaDto(patient);
                    mapa[dto.Id] = new PacienteResumo(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Cns,
                        dto.DataNascimento, dto.Sexo, dto.TelefoneVerificado, dto.Endereco?.Cep,
                        dto.TelefoneCelular ?? dto.TelefoneVerificado ?? dto.TelefonePrincipal, dto.Endereco?.Logradouro);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Mesma régua do ResolverAsync: falha/lentidão do hub degrada; os ids do
                // chunk caem no fallback individual abaixo.
                logger.LogWarning(ex, "Busca em lote de pacientes no hub FHIR falhou — caindo no fallback individual.");
            }
        }

        // Fallback: o que o lote não devolveu (hub antigo sem _id, paciente excluído,
        // chunk que falhou) tenta individualmente — cada miss real degrada para null.
        var faltantes = distintos.Where(id => !mapa.ContainsKey(id)).ToArray();
        if (faltantes.Length > 0)
        {
            var resumos = await Task.WhenAll(faltantes.Select(id => ResolverAsync(id, ct)));
            foreach (var r in resumos)
                if (r is not null) mapa[r.Id] = r;
        }
        return mapa;
    }

    public async Task<IReadOnlySet<Guid>> BuscarIdsPorTermoAsync(string termo, int limite = 50, CancellationToken ct = default)
    {
        var ids = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(termo)) return ids;

        try
        {
            // Uma chamada só: o hub casa nome (contém, sem acento/caso) OU CPF/CNS por prefixo
            // e ordena prefixo-primeiro. Antes eram duas buscas (name + identifier) e a de nome
            // era cortada em 50 alfabéticos — origem do ticket #91.
            ColetarIds(await fhir.BuscarPorTermoAsync(termo.Trim(), limite, ct), ids);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Busca de pacientes por termo no hub FHIR falhou — seguindo sem ids.");
        }
        return ids;
    }

    private static void ColetarIds(Bundle? bundle, HashSet<Guid> ids)
    {
        if (bundle?.Entry is null) return;
        foreach (var e in bundle.Entry)
            if (e.Resource is Patient p && Guid.TryParse(p.Id, out var g))
                ids.Add(g);
    }
}
