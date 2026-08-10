using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Auditoria;
using SMSMarica.Core.Integracoes.Pep;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser.Pacientes;

/// <summary>O que aconteceu com um paciente na conciliação.</summary>
public enum ResultadoConciliacaoSer
{
    /// <summary>O hub já tinha tudo que o SER sabe — nada foi escrito.</summary>
    Inalterado = 0,

    /// <summary>O paciente não existia no hub e foi criado.</summary>
    Criado = 1,

    /// <summary>O paciente existia e ganhou dado que faltava.</summary>
    Enriquecido = 2,

    /// <summary>Sem CPF válido e sem CNS: não há chave nenhuma. Fica de fora (1 caso em 25.439).</summary>
    SemChave = 3,
}

public sealed record ConciliacaoSerDto(
    ResultadoConciliacaoSer Resultado, Guid? PacienteId, IReadOnlyList<string> Mudancas);

/// <summary>
/// Leva o paciente que o SER conhece para o hub FHIR.
///
/// <para><b>Pela porta canônica, não por uma nova.</b> Escreve por
/// <see cref="UpsertCanonicoPep"/> — o mesmo caminho de Salux e Klinikos. É de lá que vêm, de
/// graça e idênticos aos outros conectores: dedup por CPF válido, união de identifiers (o CPF
/// entra em quem só tinha CNS e vice-versa, sem apagar o rastro das outras bases), preservação do
/// que o painel editou, telefone verificado intocável, congelamento de nascimento divergente,
/// concorrência otimista e guarda de no-op.</para>
///
/// <para><b>Em modo fonte parcial</b> (<c>fonteParcial: true</c>): o SER é um formulário, não um
/// prontuário. Campo vazio significa "não perguntaram" e é completado do próprio hub — o SER
/// acrescenta, nunca remove.</para>
///
/// <para><b>Âncora: CPF válido por dígito verificador; sem ele, o CNS.</b> O CPF é a chave
/// CERTA porque é uma por pessoa. O CNS não: a mesma pessoa pode ter mais de um número, e o SER
/// usa CNS provisório (faixa 898…). Medido no piloto de 10/08/2026 — dos 40 pacientes criados
/// ancorados em CNS, <b>17 tinham no hub alguém de mesmo nome e mesma data de nascimento</b>.
/// Ou seja: ancorar por CNS duplica pessoa em cerca de 4 de cada 10 casos.</para>
///
/// <para><b>Mesmo assim eles entram</b> — e marcados (ADR-0041): o hub afirmar por omissão que
/// aquele cidadão não existe é pior que um registro duplicado e declarado. A tag
/// <c>urn:smsmarica:qualidade|identidade-incompleta</c> é o que torna essa dívida <b>buscável</b>
/// (<c>GET /fhir/Patient?_tag=…</c>), em vez de invisível. O dia em que o CPF aparecer, a ponte
/// local→CPF do upsert canônico reaproveita o mesmo recurso e a tag sai sozinha.</para>
/// </summary>
public interface ISerConciliacaoPacienteService
{
    Task<ConciliacaoSerDto> ConciliarAsync(SerSolicitacao solicitacao, CancellationToken ct);
}

public sealed class SerConciliacaoPacienteService(
    IHubFhirEscritor escritor,
    IAuditoriaService auditoria,
    ILogger<SerConciliacaoPacienteService> logger) : ISerConciliacaoPacienteService
{
    public async Task<ConciliacaoSerDto> ConciliarAsync(
        SerSolicitacao s, CancellationToken ct)
    {
        var cpf = CpfPep.Valido(s.Cpf) ? CpfPep.Digitos(s.Cpf) : string.Empty;
        var cns = new string([.. (s.Cns ?? string.Empty).Where(char.IsDigit)]);

        // Sem chave nenhuma não há o que casar: criar geraria um Patient que o próximo ciclo não
        // reencontraria — duplicata NOVA a cada rodada, que é outro problema, bem pior.
        if (cpf.Length == 0 && cns.Length == 0)
        {
            return new ConciliacaoSerDto(ResultadoConciliacaoSer.SemChave, null, []);
        }

        // CPF quando dá; senão CNS, e aí o mapper carimba identidade-incompleta.
        var (system, valor) = cpf.Length > 0
            ? (SerPacienteFhirMapper.SysCpf, cpf)
            : (SerPacienteFhirMapper.SysCns, cns);

        var novo = SerPacienteFhirMapper.Construir(s);
        var ctx = Contexto();

        Resource? antes = null;
        var escreveu = false;

        var id = await UpsertCanonicoPep.UpsertAsync(
            ctx, "Patient", system, valor, novo,
            // O SER não tem código interno de paciente: a chave dele JÁ é nacional. Passar o
            // próprio system da âncora desliga a ponte "local→CPF", que aqui não faz sentido.
            systemCdInterno: system,
            ct,
            fonteParcial: true,
            aoEscrever: (atual, _) => { antes = atual; escreveu = true; });

        var pacienteId = Guid.TryParse(id, out var g) ? g : (Guid?)null;

        if (!escreveu)
        {
            return new ConciliacaoSerDto(ResultadoConciliacaoSer.Inalterado, pacienteId, []);
        }

        var mudancas = Diferencas(antes as Patient, novo);
        var resultado = antes is null
            ? ResultadoConciliacaoSer.Criado
            : ResultadoConciliacaoSer.Enriquecido;

        await RegistrarTrilhaAsync(pacienteId, resultado, mudancas, s, ct);

        logger.LogInformation(
            "SER/paciente {Id}: {Resultado} ({Qtd} campo(s)) a partir da solicitação {IdSer}.",
            pacienteId, resultado, mudancas.Count, s.IdSer);

        return new ConciliacaoSerDto(resultado, pacienteId, mudancas);
    }

    /// <summary>
    /// Contexto mínimo. <see cref="UpsertCanonicoPep"/> só usa <c>Escritor</c>, <c>Progresso</c>,
    /// <c>Falhas</c>, <c>Divergencias</c> e <c>DivergenciasConhecidas</c> — conferido no fonte, não
    /// presumido. <c>Opcoes</c> e <c>Marca</c> são exigidos pelo tipo e ficam inertes: não existe
    /// "modo de sincronização" nem marca d'água aqui, porque a origem é a nossa própria tabela
    /// espelho e quem decide o recorte é quem chama.
    ///
    /// <para><c>Divergencias</c> nulo é decisão, não esquecimento: a fila de arbitragem de
    /// nascimento é alimentada pelos conectores de PEP, que leem o dado do prontuário. O SER
    /// traz o que alguém digitou num pedido — congelar (o upsert continua fazendo) sim, abrir
    /// disputa formal contra o Salux, não.</para>
    /// </summary>
    private ContextoImportacaoPep Contexto() => new()
    {
        Escritor = escritor,
        Progresso = new ProgressoImportacao(),
        BaseSlug = SerPacienteFhirMapper.Slug,
        Opcoes = new OpcoesImportacao(
            ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo,
            MaxMedicos: null, MaxPacientes: null, ApagarAntes: false),
        Marca = new MarcaDagua(),
    };

    /// <summary>
    /// O que efetivamente mudou, em linguagem de gente — é isto que vai para a trilha. Compara o
    /// estado do hub ANTES com o recurso que está sendo gravado; em criação, lista o que o SER
    /// trouxe.
    /// </summary>
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

        foreach (var sys in new[] { SerPacienteFhirMapper.SysCpf, SerPacienteFhirMapper.SysCns })
        {
            Comparar(sys.EndsWith("cpf", StringComparison.Ordinal) ? "CPF" : "CNS",
                Ident(antes, sys), Ident(depois, sys));
        }

        // Telefone entra por número, não por slot: o que importa na trilha é "passou a ter este
        // número", e o slot pode ser reorganizado pelo merge sem nada de novo ter entrado.
        var fonesAntes = Fones(antes);
        var fonesDepois = Fones(depois);
        foreach (var f in fonesDepois.Where(f => !fonesAntes.Contains(f)))
        {
            m.Add($"telefone: + {f}");
        }

        // Canário, não relatório: o merge não pode remover contato (PreservarContatos), então esta
        // linha nunca deveria sair. Ela existe porque a trilha só listava telefone ACRESCENTADO —
        // e foi por isso que o apagamento de 10/08/2026 passou 20 minutos sem deixar rastro.
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

    /// <summary>
    /// Trilha de auditoria. <b>Uma entrada por paciente conciliado</b>, com a lista do que mudou —
    /// não uma por campo: quem lê a trilha quer ver "o SER completou o cadastro do fulano em
    /// 10/08", e não seis linhas soltas do mesmo instante.
    ///
    /// <para>O "quem" é resolvido pelo <c>IUsuarioAtualAccessor</c>; no backfill e na varredura
    /// não há usuário logado e o registro sai com autor nulo. Por isso a AÇÃO diz a origem — sem
    /// isso a trilha mostraria uma alteração sem responsável nem procedência.</para>
    /// </summary>
    private async Task RegistrarTrilhaAsync(
        Guid? pacienteId, ResultadoConciliacaoSer resultado, IReadOnlyList<string> mudancas,
        SerSolicitacao s, CancellationToken ct)
    {
        if (pacienteId is null || mudancas.Count == 0) return;

        var acao = resultado == ResultadoConciliacaoSer.Criado
            ? "Criado pela conciliação com o SER"
            : "Completado pela conciliação com o SER";

        await auditoria.RegistrarAsync(
            entidade: "Paciente",
            entidadeId: pacienteId.Value.ToString(),
            acao: acao,
            valorAnterior: null,
            valorNovo: $"Solicitação SER {s.IdSer}: " + string.Join(" · ", mudancas),
            cancellationToken: ct);
    }
}
