using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Integracoes.Pep;
using SMSMais.Core.Integracoes.Pep.Estrategias;
using SMSMais.Core.Integracoes.Pep.Fhir;
using SMSMais.Core.Integracoes.Pep.Progresso;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
// Alias: o namespace EsusSg.Pacientes sombreia SMSMais.Core.Pacientes.
using PatientMergeFhir = SMSMais.Core.Pacientes.Fhir.PatientMergeFhir;

namespace SMSMais.Core.EsusSg.Pacientes;

/// <summary>
/// Monta o <c>Patient</c> FHIR a partir de um pedido do ESUS SG — um registro por fonte, com
/// <c>meta.source</c> próprio (ADR-0009). Sem CPF válido o paciente entra MARCADO como identidade
/// incompleta, não fica de fora (ADR-0041).
/// </summary>
public static class EsusSgPacienteFhirMapper
{
    public const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";
    public const string SysCns = "https://fhir.saude.gov.br/sid/cns";

    public const string Slug = "esussg-saogoncalo";

    /// <summary><c>smsmarica</c> é carve-out do ADR-0046 (<c>meta.source</c> não é renomeado).</summary>
    public const string Source = "https://smsmarica.saude.marica/source/" + Slug;

    public static Patient Construir(EsusSgSolicitacao s)
    {
        var cpf = CpfPep.Valido(s.Cpf) ? CpfPep.Digitos(s.Cpf) : string.Empty;
        var cns = Digitos(s.Cns);

        var ident = new List<Identifier>();
        if (cpf.Length > 0) ident.Add(new Identifier(SysCpf, cpf));
        if (cns.Length > 0) ident.Add(new Identifier(SysCns, cns));

        var p = new Patient { Meta = new Meta { Source = Source }, Identifier = ident, Active = true };

        if (Texto(s.PacienteNome) is { } nome && !nome.StartsWith('('))
            p.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }];

        if (Genero(s.Sexo) is { } sexo) p.Gender = sexo;
        if (s.DataNascimento is { } nasc) p.BirthDate = nasc.ToString("yyyy-MM-dd");

        // O ESUS dá "telefone" (formatado) e "celular" (do cadastro de notificação). Contato só
        // acumula no hub — nunca apaga o que outra fonte trouxe.
        PatientMergeFhir.AplicarContatos(
            p, principal: s.Telefone, celular: s.Celular, residencial: null, email: null, manual: false);

        if (Texto(s.NomeMae) is { } mae) PatientMergeFhir.UpsertContato(p, "MTH", mae);

        if (Texto(s.Bairro) is not null || Texto(s.MunicipioPaciente) is not null)
        {
            var a = new Address { Use = Address.AddressUse.Home, Type = Address.AddressType.Physical };
            if (Texto(s.Bairro) is { } bairro) a.District = bairro;
            if (Texto(s.MunicipioPaciente) is { } cidade) a.City = cidade;
            p.Address = [a];
        }

        if (cpf.Length == 0)
        {
            p.Meta.Tag ??= [];
            const string sys = "urn:smsmarica:qualidade";
            const string cod = "identidade-incompleta";
            if (!p.Meta.Tag.Any(t => t.System == sys && t.Code == cod))
                p.Meta.Tag.Add(new Coding(sys, cod) { Display = "Sem CPF — não é possível unir a outras bases" });
        }

        return p;
    }

    private static AdministrativeGender? Genero(string? sexo) =>
        (sexo ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "M" or "MASCULINO" => AdministrativeGender.Male,
            "F" or "FEMININO" => AdministrativeGender.Female,
            _ => null,
        };

    private static string? Texto(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}

public enum ResultadoConciliacaoEsusSg
{
    Inalterado = 0,
    Criado = 1,
    Enriquecido = 2,
    SemChave = 3,
}

public sealed record ConciliacaoEsusSgDto(ResultadoConciliacaoEsusSg Resultado, Guid? PacienteId, int Mudancas);

public sealed record BackfillPacientesEsusSgDto(
    int Pacientes, int Criados, int Enriquecidos, int Inalterados, int SemChave, int Falhas, int DuracaoSegundos);

/// <summary>
/// Leva o paciente de um pedido do ESUS SG ao hub FHIR pelo upsert canônico dos PEPs — o mesmo
/// caminho do SERNIT: âncora no CPF válido, senão no CNS (com a marca de identidade incompleta);
/// ponte CNS→CPF LIGADA (a fila do ESUS traz os dois, e um pedido antigo só com CNS reaproveita o
/// paciente quando o CPF aparece, sem duplicar).
/// </summary>
public interface IEsusSgConciliacaoPacienteService
{
    Task<ConciliacaoEsusSgDto> ConciliarAsync(EsusSgSolicitacao solicitacao, CancellationToken ct);

    /// <summary>Drena os pedidos carimbados com <c>PacienteConciliarEm</c> (o runner chama).</summary>
    Task<BackfillPacientesEsusSgDto> ExecutarPendentesAsync(int limite, CancellationToken ct);
}

public sealed class EsusSgConciliacaoPacienteService(
    SmsMaisDbContext db,
    IHubFhirEscritor escritor,
    IAuditoriaService auditoria,
    ILogger<EsusSgConciliacaoPacienteService> logger) : IEsusSgConciliacaoPacienteService
{
    public async Task<ConciliacaoEsusSgDto> ConciliarAsync(EsusSgSolicitacao s, CancellationToken ct)
    {
        var cpf = CpfPep.Valido(s.Cpf) ? CpfPep.Digitos(s.Cpf) : string.Empty;
        var cns = new string([.. (s.Cns ?? string.Empty).Where(char.IsDigit)]);
        if (cpf.Length == 0 && cns.Length == 0)
        {
            return new ConciliacaoEsusSgDto(ResultadoConciliacaoEsusSg.SemChave, null, 0);
        }

        var (system, valor) = cpf.Length > 0
            ? (EsusSgPacienteFhirMapper.SysCpf, cpf)
            : (EsusSgPacienteFhirMapper.SysCns, cns);

        var novo = EsusSgPacienteFhirMapper.Construir(s);
        Resource? antes = null;
        var escreveu = false;

        var id = await UpsertCanonicoPep.UpsertAsync(
            new ContextoImportacaoPep
            {
                Escritor = escritor,
                Progresso = new ProgressoImportacao(),
                BaseSlug = EsusSgPacienteFhirMapper.Slug,
                Opcoes = new OpcoesImportacao(
                    ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo,
                    MaxMedicos: null, MaxPacientes: null, ApagarAntes: false),
                Marca = new MarcaDagua(),
            },
            "Patient", system, valor, novo,
            systemCdInterno: EsusSgPacienteFhirMapper.SysCns,
            ct,
            fonteParcial: true,
            aoEscrever: (atual, _) => { antes = atual; escreveu = true; });

        var pacienteId = Guid.TryParse(id, out var g) ? g : (Guid?)null;
        if (!escreveu) return new ConciliacaoEsusSgDto(ResultadoConciliacaoEsusSg.Inalterado, pacienteId, 0);

        var resultado = antes is null ? ResultadoConciliacaoEsusSg.Criado : ResultadoConciliacaoEsusSg.Enriquecido;
        if (pacienteId is { } pid)
        {
            await auditoria.RegistrarAsync(
                entidade: "Paciente",
                entidadeId: pid.ToString(),
                acao: resultado == ResultadoConciliacaoEsusSg.Criado
                    ? "Criado pela conciliação com o ESUS de São Gonçalo"
                    : "Completado pela conciliação com o ESUS de São Gonçalo",
                valorAnterior: null,
                valorNovo: $"Pedido ESUS SG {s.IdEsusSg} ({s.Recurso})",
                cancellationToken: ct);
        }

        return new ConciliacaoEsusSgDto(resultado, pacienteId, 1);
    }

    public async Task<BackfillPacientesEsusSgDto> ExecutarPendentesAsync(int limite, CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        int criados = 0, enriquecidos = 0, inalterados = 0, semChave = 0, falhas = 0;

        var pendentes = await db.EsusSgSolicitacoes
            .Where(s => s.ExcluidoEm == null && s.PacienteConciliarEm != null)
            .OrderBy(s => s.PacienteConciliarEm)
            .Take(limite)
            .ToListAsync(ct);
        if (pendentes.Count == 0) return new BackfillPacientesEsusSgDto(0, 0, 0, 0, 0, 0, 0);

        foreach (var s in pendentes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var r = await ConciliarAsync(s, ct);
                switch (r.Resultado)
                {
                    case ResultadoConciliacaoEsusSg.Criado: criados++; break;
                    case ResultadoConciliacaoEsusSg.Enriquecido: enriquecidos++; break;
                    case ResultadoConciliacaoEsusSg.Inalterado: inalterados++; break;
                    case ResultadoConciliacaoEsusSg.SemChave: semChave++; break;
                }
                if (r.PacienteId is { } pid) s.PacienteId = pid;

                // Limpa a marca (inclusive "sem chave"): se o ESUS trouxer CPF/CNS depois, o
                // retrato do paciente muda e a varredura remarca sozinha.
                s.PacienteConciliarEm = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                // Vai para o FIM da fila — retenta sem bloquear os outros.
                s.PacienteConciliarEm = DateTime.UtcNow;
                logger.LogWarning(ex, "ESUS SG/conciliação: falhou no paciente do pedido {Id}.", s.IdEsusSg);
            }
        }

        await db.SaveChangesAsync(ct);
        var duracao = (int)(DateTime.UtcNow - inicio).TotalSeconds;
        logger.LogInformation(
            "ESUS SG/conciliação: {Qtd} pendente(s) — {Criados} criados, {Enriq} enriquecidos, {Inalt} inalterados, "
            + "{SemChave} sem chave, {Falhas} falhas em {Seg}s.",
            pendentes.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);
        return new BackfillPacientesEsusSgDto(pendentes.Count, criados, enriquecidos, inalterados, semChave, falhas, duracao);
    }
}

/// <summary>Drena a fila de conciliação de pacientes do ESUS SG a cada 10 min (padrão SERNIT).</summary>
public sealed class EsusSgConciliacaoPacienteRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<EsusSgConciliacaoPacienteRunner> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);
    private const int Lote = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var total = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var escopo = scopeFactory.CreateScope();
                    var svc = escopo.ServiceProvider.GetRequiredService<IEsusSgConciliacaoPacienteService>();
                    var r = await svc.ExecutarPendentesAsync(Lote, stoppingToken);
                    if (r.Pacientes == 0) break;
                    total += r.Pacientes;
                    if (r.Falhas == r.Pacientes)
                    {
                        logger.LogWarning(
                            "ESUS SG/conciliação: lote inteiro falhou ({Qtd}). Parando para não girar em falso.", r.Falhas);
                        break;
                    }
                }
                if (total > 0) logger.LogInformation("ESUS SG/conciliação: fila drenada — {Total} paciente(s).", total);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ESUS SG/conciliação: passagem falhou; tenta de novo em 10 min.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
