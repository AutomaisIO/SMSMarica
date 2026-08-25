using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Integracoes.Pep;
using SMSMais.Core.Integracoes.Pep.Estrategias;
using SMSMais.Core.Integracoes.Pep.Fhir;
using SMSMais.Core.Integracoes.Pep.Progresso;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit.Pacientes;

/// <summary>O que aconteceu com um paciente na conciliação.</summary>
public enum ResultadoConciliacaoSernit
{
    Inalterado = 0,
    Criado = 1,
    Enriquecido = 2,
    /// <summary>Sem CPF válido e sem CNS: não há chave nenhuma. Fica de fora.</summary>
    SemChave = 3,
}

public sealed record ConciliacaoSernitDto(
    ResultadoConciliacaoSernit Resultado, Guid? PacienteId, IReadOnlyList<string> Mudancas);

/// <summary>
/// Leva o paciente que o SERNIT conhece para o hub FHIR, pela porta canônica
/// (<see cref="UpsertCanonicoPep"/>) — igual a Salux/Klinikos/SER-RJ. Âncora: CPF válido; sem ele,
/// o CNS (e aí o mapper carimba identidade-incompleta, ADR-0041). Modo fonte parcial: o SERNIT só
/// acrescenta, nunca remove.
/// </summary>
public interface ISernitConciliacaoPacienteService
{
    Task<ConciliacaoSernitDto> ConciliarAsync(SernitSolicitacao solicitacao, CancellationToken ct);
}

public sealed class SernitConciliacaoPacienteService(
    IHubFhirEscritor escritor,
    IAuditoriaService auditoria,
    ILogger<SernitConciliacaoPacienteService> logger) : ISernitConciliacaoPacienteService
{
    public async Task<ConciliacaoSernitDto> ConciliarAsync(SernitSolicitacao s, CancellationToken ct)
    {
        var cpf = CpfPep.Valido(s.Cpf) ? CpfPep.Digitos(s.Cpf) : string.Empty;
        var cns = new string([.. (s.Cns ?? string.Empty).Where(char.IsDigit)]);

        if (cpf.Length == 0 && cns.Length == 0)
        {
            return new ConciliacaoSernitDto(ResultadoConciliacaoSernit.SemChave, null, []);
        }

        var (system, valor) = cpf.Length > 0
            ? (SernitPacienteFhirMapper.SysCpf, cpf)
            : (SernitPacienteFhirMapper.SysCns, cns);

        var novo = SernitPacienteFhirMapper.Construir(s);
        var ctx = Contexto();

        Resource? antes = null;
        var escreveu = false;

        var id = await UpsertCanonicoPep.UpsertAsync(
            ctx, "Patient", system, valor, novo,
            systemCdInterno: system,
            ct,
            fonteParcial: true,
            aoEscrever: (atual, _) => { antes = atual; escreveu = true; });

        var pacienteId = Guid.TryParse(id, out var g) ? g : (Guid?)null;

        if (!escreveu)
        {
            return new ConciliacaoSernitDto(ResultadoConciliacaoSernit.Inalterado, pacienteId, []);
        }

        var mudancas = Diferencas(antes as Patient, novo);
        var resultado = antes is null
            ? ResultadoConciliacaoSernit.Criado
            : ResultadoConciliacaoSernit.Enriquecido;

        await RegistrarTrilhaAsync(pacienteId, resultado, mudancas, s, ct);

        logger.LogInformation(
            "SERNIT/paciente {Id}: {Resultado} ({Qtd} campo(s)) a partir da solicitação {IdSernit}.",
            pacienteId, resultado, mudancas.Count, s.IdSernit);

        return new ConciliacaoSernitDto(resultado, pacienteId, mudancas);
    }

    private ContextoImportacaoPep Contexto() => new()
    {
        Escritor = escritor,
        Progresso = new ProgressoImportacao(),
        BaseSlug = SernitPacienteFhirMapper.Slug,
        Opcoes = new OpcoesImportacao(
            ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo,
            MaxMedicos: null, MaxPacientes: null, ApagarAntes: false),
        Marca = new MarcaDagua(),
    };

    private static List<string> Diferencas(Patient? antes, Patient depois)
    {
        var m = new List<string>();

        void Comparar(string campo, string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(b) || string.Equals(a, b, StringComparison.Ordinal)) return;
            m.Add(string.IsNullOrWhiteSpace(a) ? $"{campo}: (vazio) → {b}" : $"{campo}: {a} → {b}");
        }

        Comparar("nome", Oficial(antes), Oficial(depois));
        Comparar("nascimento", antes?.BirthDate, depois.BirthDate);
        Comparar("sexo", antes?.Gender?.ToString(), depois.Gender?.ToString());
        Comparar("mãe", Mae(antes), Mae(depois));
        Comparar("endereço", Endereco(antes), Endereco(depois));

        foreach (var sys in new[] { SernitPacienteFhirMapper.SysCpf, SernitPacienteFhirMapper.SysCns })
        {
            Comparar(sys.EndsWith("cpf", StringComparison.Ordinal) ? "CPF" : "CNS",
                Ident(antes, sys), Ident(depois, sys));
        }

        var fonesAntes = Fones(antes);
        var fonesDepois = Fones(depois);
        foreach (var f in fonesDepois.Where(f => !fonesAntes.Contains(f)))
        {
            m.Add($"telefone: + {f}");
        }

        foreach (var f in fonesAntes.Where(f => !fonesDepois.Contains(f)))
        {
            m.Add($"ATENÇÃO telefone REMOVIDO: {f}");
        }

        return m;
    }

    private static string? Oficial(Patient? p) =>
        p?.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text;

    private static string? Ident(Patient? p, string system) =>
        p?.Identifier?.FirstOrDefault(i => i.System == system)?.Value;

    private static string? Mae(Patient? p) =>
        p?.Contact?.FirstOrDefault(c => c.Relationship?
            .Any(r => r.Coding?.Any(cd => cd.Code == "MTH") == true) == true)?.Name?.Text;

    private static string? Endereco(Patient? p)
    {
        var a = p?.Address?.FirstOrDefault();
        if (a is null) return null;
        var partes = new[] { string.Join(' ', a.Line ?? []), a.District, a.City, a.State, a.PostalCode };
        return string.Join(", ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static HashSet<string> Fones(Patient? p) =>
        [.. (p?.Telecom ?? [])
            .Where(t => t.System == ContactPoint.ContactPointSystem.Phone)
            .Select(t => new string([.. (t.Value ?? string.Empty).Where(char.IsDigit)]))
            .Where(v => v.Length >= 8)];

    private async Task RegistrarTrilhaAsync(
        Guid? pacienteId, ResultadoConciliacaoSernit resultado, IReadOnlyList<string> mudancas,
        SernitSolicitacao s, CancellationToken ct)
    {
        if (pacienteId is null || mudancas.Count == 0) return;

        var acao = resultado == ResultadoConciliacaoSernit.Criado
            ? "Criado pela conciliação com o SERNIT"
            : "Completado pela conciliação com o SERNIT";

        await auditoria.RegistrarAsync(
            entidade: "Paciente",
            entidadeId: pacienteId.Value.ToString(),
            acao: acao,
            valorAnterior: null,
            valorNovo: $"Solicitação SERNIT {s.IdSernit}: " + string.Join(" · ", mudancas),
            cancellationToken: ct);
    }
}
