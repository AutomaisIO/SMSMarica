using System.Globalization;
using Hl7.Fhir.Model;
using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Fhir;

/// <summary>
/// Upsert <b>canônico</b> de Patient/Practitioner no hub — o caminho ÚNICO pelo qual identidade
/// de pessoa entra, seja qual for o PEP de origem. Nasceu dentro do conector do Salux e foi
/// extraído em 04/08/2026, no dia em que o conector do Klinikos tentou entrar pelo atalho dos
/// recursos clínicos (<c>PUT ?identifier=</c>) e o hub respondeu 405 em 606 mil pacientes:
/// aqueles dois controllers não aceitam update condicional <b>de propósito</b>, exatamente para
/// que nenhum conector consiga escrever identidade sem passar por aqui.
///
/// <para>O que "passar por aqui" garante, na ordem:</para>
/// <list type="number">
/// <item><b>Dedup pela chave nacional.</b> Busca por identifier (CPF via
/// <see cref="SysCpf"/>; sem CPF, a chave local da base). Achou → atualiza o MESMO recurso;
/// não achou → cria. A mesma pessoa vista por Salux e Klinikos converge para um só Patient.</item>
/// <item><b>Identifiers acumulam</b> (<see cref="UnirIdentifiers"/>): o código interno de cada
/// base entra no recurso e nenhum apaga o do outro — é o rastro ADR-0009.</item>
/// <item><b>Merge/preserve</b> (ADR-0020): reimport não sobrescreve campos editados no painel
/// nem telefone verificado — <see cref="Pacientes.Fhir.PatientMergeFhir.PreservarDoExistente"/>.</item>
/// <item><b>Conciliação de identidade</b>: mesmo CPF com nascimento diferente NÃO sobrescreve —
/// congela o valor do hub e registra a divergência para a arbitragem (ADR-0039 §3.2).</item>
/// <item><b>Concorrência otimista</b>: If-Match; se o painel editou no meio, re-lê e re-mergeia.</item>
/// <item><b>Guarda de no-op</b> (<see cref="Identico"/>): se o recurso montado é igual ao que já
/// está no hub, o PUT não sai.</item>
/// </list>
/// </summary>
internal static class UpsertCanonicoPep
{
    /// <summary>System do CPF — a chave nacional que une a mesma pessoa entre PEPs.</summary>
    public const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";

    /// <summary>
    /// Prefixo dos systems de conselho profissional. É prefixo porque cada mapper especializa
    /// (<c>urn:br:conselho:crm</c> no Klinikos, <c>urn:br:conselho:crm:RJ</c> no Salux).
    /// </summary>
    private const string SysConselhoPrefixo = "urn:br:conselho:";

    /// <param name="systemCdInterno">
    /// System do identifier INTERNO da base de origem (ex.: <c>urn:salux:cd_paciente</c>,
    /// <c>urn:klinikos:paciente</c>) — usado só para extrair o código nativo que rotula a
    /// divergência na fila de arbitragem.
    /// </param>
    /// <param name="fonteParcial">
    /// A origem conhece só um PEDAÇO da pessoa (o SER, por exemplo, traz endereço em 77% das
    /// solicitações e telefone em 46%). Com <c>true</c>, campo vazio no recurso montado significa
    /// "não sei" e é completado do hub — a fonte só acrescenta. Com <c>false</c> (padrão, e o caso
    /// de Salux e Klinikos) a origem é prontuário completo: campo que sumiu lá some aqui.
    /// </param>
    /// <param name="aoEscrever">
    /// Chamado imediatamente antes de cada escrita, com (estado no hub, recurso a gravar) — e
    /// <c>null</c> no primeiro argumento quando é criação. Existe para a trilha de auditoria
    /// poder dizer o que MUDOU sem uma segunda ida ao hub: quem já tem os dois lados aqui é este
    /// método. Não é chamado quando a guarda de no-op decide não escrever.
    /// </param>
    public static async Task<string> UpsertAsync(
        ContextoImportacaoPep ctx, string tipo, string system, string valor,
        Resource novo, string systemCdInterno, CancellationToken ct, bool fonteParcial = false,
        Action<Resource?, Resource>? aoEscrever = null)
    {
        var existentes = await ctx.Escritor.BuscarPorIdentifierAsync(tipo, system, valor, ct);
        var atual = existentes.Entry.Select(e => e.Resource).FirstOrDefault(r => r is not null);

        // PONTE local→CPF: paciente importado SEM CPF ganhou CPF na origem. Duas situações,
        // e a segunda me escapou na primeira versão:
        //
        //   (a) a busca por CPF não acha nada → o recurso EXISTE sob a chave local desta base.
        //       Reaproveita: ganha o CPF, e a tag de identidade incompleta sai naturalmente.
        //
        //   (b) a busca por CPF ACHA — mas num recurso de OUTRA base (a mesma pessoa já estava
        //       no hub pelo Salux). O registro que esta base criou sob a chave local continua
        //       lá, órfão, com parte do histórico. Medido em 05/08: 2 pacientes da UPA com o
        //       prontuário partido assim. Aqui isso vira FALHA REGISTRADA — a fusão de dois
        //       recursos com clínica pendurada é cirurgia (repontar referências, apagar o
        //       perdedor) e não pode ser improvisada no caminho quente do upsert. O que não
        //       pode é seguir invisível.
        if (system == SysCpf && novo is Patient pNovo
            && pNovo.Identifier?.FirstOrDefault(i => i.System == systemCdInterno)?.Value is { Length: > 0 } chaveLocal)
        {
            var porLocal = await ctx.Escritor.BuscarPorIdentifierAsync(tipo, systemCdInterno, chaveLocal, ct);
            var local = porLocal.Entry.Select(e => e.Resource).FirstOrDefault(r => r is not null);

            if (atual is null)
            {
                atual = local;                                   // (a)
            }
            else if (local is not null && local.Id != atual.Id)  // (b)
            {
                var aviso =
                    $"paciente {chaveLocal}: prontuário partido — o CPF resolve em Patient/{atual.Id} "
                    + $"mas a chave local desta base aponta Patient/{local.Id}. "
                    + "Precisa de mesclagem (repontar clínica e remover o duplicado).";
                // Nos DOIS canais: o painel mostra que houve, a trilha durável diz quais.
                ctx.Progresso.RegistrarFalha(0, aviso);
                ctx.Falhas?.Registrar(0, aviso);
            }
        }

        for (var tentativa = 1; atual is not null; tentativa++)
        {
            UnirIdentifiers(novo, atual);
            if (novo is Patient np && atual is Patient ap)
            {
                // A união acabou de acontecer: se o CPF entrou (desta base ou de outra), a marca
                // de "sem CPF" que o mapper carimbou sobre a PRÓPRIA leitura não vale mais.
                Pacientes.Fhir.PatientMergeFhir.RevisarIdentidadeIncompleta(np);
                Pacientes.Fhir.PatientMergeFhir.PreservarDoExistente(np, ap);
                // Antes da conciliação, e sem atrapalhá-la: CompletarVazios só copia onde a
                // origem NADA disse. Divergência de verdade é origem e hub ambos preenchidos e
                // diferentes — esse caso passa intacto para ConciliarNascimento congelar.
                if (fonteParcial) Pacientes.Fhir.PatientMergeFhir.CompletarVazios(np, ap);
                PreservarClinicoEntreBases(np, ap);
                ConciliarNascimento(ctx, system, valor, np, ap, systemCdInterno);
            }
            novo.Id = atual.Id;
            novo.Meta ??= new Meta();
            novo.Meta.VersionId = atual.Meta?.VersionId;

            // Nada mudou → não escreve. O ciclo incremental relê de propósito um bloco fixo de
            // pessoas todo poll (internação em curso é re-lida a cada ciclo — ADR-0025; médicos
            // são re-scan integral), e sem esta guarda cada poll gravava um PUT idêntico ao
            // anterior: medido em 06/08, pacientes internados chegaram a version_id 226, ~150
            // pacientes + ~750 médicos reescritos a cada 11 minutos. Não corrompia nada (o merge
            // preserva), mas queimava escrita no hub e enchia o histórico de versões de ruído,
            // deixando `last_updated` sem significado para auditoria.
            if (Identico(novo, atual))
            {
                Interlocked.Increment(ref tipo == "Practitioner"
                    ? ref ctx.Progresso.MedicosInalterados
                    : ref ctx.Progresso.PacientesInalterados);
                return atual.Id!;
            }

            try
            {
                aoEscrever?.Invoke(atual, novo);
                var atualizado = await ctx.Escritor.AtualizarAsync(tipo, atual.Id!, novo, ct);
                return atualizado.Id!;
            }
            catch (Pacientes.Fhir.ConflitoVersaoHubException) when (tentativa < 3)
            {
                var refetch = await ctx.Escritor.BuscarPorIdentifierAsync(tipo, system, valor, ct);
                atual = refetch.Entry.Select(e => e.Resource).FirstOrDefault(r => r is not null);
            }
        }

        aoEscrever?.Invoke(null, novo);
        var criado = await ctx.Escritor.CriarAsync(novo, ct);
        return criado.Id!;
    }

    /// <summary>
    /// O recurso montado é idêntico ao que já está no hub? Compara o JSON FHIR inteiro, com
    /// <c>meta.versionId</c> e <c>meta.lastUpdated</c> zerados dos dois lados — são exatamente
    /// os campos que o hub carimba a cada escrita, e compará-los faria toda comparação falhar.
    /// Todo o resto participa, <c>meta.source</c> inclusive: se a pessoa passou a ser vista por
    /// outra base, isso É uma mudança e o PUT deve sair.
    ///
    /// <para><b>Por que a ordem das listas não atrapalha.</b> A comparação é textual, então
    /// identifiers/telecoms em ordem diferente contariam como "mudou". Não acontece em regime:
    /// o que está no hub é o <c>novo</c> do ciclo anterior, e o build do mapper mais o
    /// <see cref="UnirIdentifiers"/> são determinísticos — a ordem converge no primeiro ciclo.
    /// No pior caso a guarda erra para o lado seguro (escreve à toa, como antes).</para>
    /// </summary>
    private static bool Identico(Resource novo, Resource atual)
    {
        if (novo.TypeName != atual.TypeName) return false;

        var (vN, luN) = (novo.Meta?.VersionId, novo.Meta?.LastUpdated);
        var (vA, luA) = (atual.Meta?.VersionId, atual.Meta?.LastUpdated);
        try
        {
            if (novo.Meta is { } mn) { mn.VersionId = null; mn.LastUpdated = null; }
            if (atual.Meta is { } ma) { ma.VersionId = null; ma.LastUpdated = null; }
            return Pacientes.Fhir.FhirJson.Serialize(novo) == Pacientes.Fhir.FhirJson.Serialize(atual);
        }
        finally
        {
            // Restaura SEMPRE: o VersionId de `novo` é o If-Match do PUT logo abaixo — perdê-lo
            // desligaria a concorrência otimista e o painel voltaria a ser sobrescrito.
            if (novo.Meta is { } mn) { mn.VersionId = vN; mn.LastUpdated = luN; }
            if (atual.Meta is { } ma) { ma.VersionId = vA; ma.LastUpdated = luA; }
        }
    }

    /// <summary>
    /// Conflito de VERDADE (não de escrita): mesmo CPF, nascimento diferente. A origem não
    /// sobrescreve o hub — congela e registra; a arbitragem (consulta oficial de CPF) decide.
    /// Só se aplica quando a âncora é o CPF: sem chave nacional não há "mesma pessoa" a conciliar.
    /// </summary>
    private static void ConciliarNascimento(
        ContextoImportacaoPep ctx, string system, string cpf, Patient novo, Patient atual,
        string systemCdInterno)
    {
        if (system != SysCpf) return;

        var origem = DataCompleta(novo.BirthDate);
        var hub = DataCompleta(atual.BirthDate);
        if (origem is null || hub is null || origem == hub) return;

        // Já conhecida: respeita a decisão vigente (congelar ou não) sem re-registrar —
        // senão um ciclo de 30 min ficaria somando ocorrência no mesmo conflito para sempre.
        if (ctx.DivergenciasConhecidas.TryGetValue(cpf, out var congelar))
        {
            if (congelar) novo.BirthDate = atual.BirthDate;
            return;
        }

        novo.BirthDate = atual.BirthDate; // congela até a arbitragem
        ctx.Divergencias?.Registrar(new DivergenciaDetectada(
            CdPaciente: CdNumerico(CodigoNativo(novo, systemCdInterno)),
            CodigoOrigem: CodigoNativo(novo, systemCdInterno),
            Cpf: cpf,
            Tipo: TipoDivergenciaIdentidade.NascimentoDivergente,
            ValorOrigem: origem,
            ValorHub: hub,
            NomeOrigem: NomeOficial(novo),
            NomeHub: NomeOficial(atual),
            PatientIdHub: atual.Id));
    }

    /// <summary>
    /// Código do paciente NA ORIGEM, extraído do identifier interno. O valor é
    /// <c>{slug}:{codigo}</c>, então o corte é no ÚLTIMO <c>:</c> — e o resultado sai tal e
    /// qual, com zeros à esquerda e tudo.
    ///
    /// <para>A versão anterior fazia "todos os dígitos do valor inteiro", o que engolia os
    /// dígitos do próprio slug (<c>upa24h</c> → <c>24</c>) e descartava zeros à esquerda.</para>
    /// </summary>
    private static string? CodigoNativo(Patient p, string systemCdInterno)
    {
        var v = p.Identifier?.FirstOrDefault(i => i.System == systemCdInterno)?.Value;
        if (string.IsNullOrWhiteSpace(v)) return null;
        var corte = v.LastIndexOf(':');
        var codigo = corte >= 0 ? v[(corte + 1)..] : v;
        return string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
    }

    /// <summary>Forma numérica do código (0 quando a origem não usa código numérico).</summary>
    private static long CdNumerico(string? codigo) =>
        long.TryParse(codigo, out var cd) ? cd : 0;

    /// <summary>Data só quando é ISO completa (<c>yyyy-MM-dd</c>) — parcial não é divergência.</summary>
    private static string? DataCompleta(string? d) =>
        d is { Length: 10 } && DateOnly.TryParseExact(
            d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? d : null;

    private static string? NomeOficial(Patient p) =>
        p.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Official)?.Text
        ?? p.Name?.FirstOrDefault()?.Text;

    private static List<Identifier> IdentificadoresDe(Resource r) => r switch
    {
        Patient p => p.Identifier ??= [],
        Practitioner pr => pr.Identifier ??= [],
        _ => [],
    };

    /// <summary>
    /// Une os identifiers do existente ao novo — nenhuma base apaga o rastro da outra. Exceção
    /// deliberada: identifier de CPF INVÁLIDO (repdigit/dígito verificador errado) do recurso
    /// antigo NÃO é copiado quando o novo não o traz — é lixo de preenchimento que poluiria a
    /// coluna de busca de CPF do hub para sempre.
    ///
    /// <para><b>A lista sai em ordem canônica (system, depois value)</b>, e isso não é estética.
    /// Cada mapper monta os identifiers DELE primeiro e a união anexa o resto no fim, então a
    /// ordem final dependia de <b>qual base escreveu por último</b>. Para quem existe em mais de
    /// uma base isso é um ping-pong sem fim: a UPA grava numa ordem, a Santa Rita regrava na
    /// outra, e a guarda de no-op (<see cref="Identico"/>) — que compara texto — via diferença
    /// onde não havia mudança nenhuma. Medido em prod 06/08: profissionais com o mesmo
    /// <c>system</c> repetido eram reescritos em 100% dos ciclos, um deles em <c>version_id</c>
    /// 585. Ordenar torna o recurso convergente: toda base produz exatamente a mesma lista.</para>
    ///
    /// <para>Ordenar é seguro aqui porque <c>identifier</c> é semanticamente um conjunto, e
    /// nenhum consumidor de Patient/Practitioner lê por índice — a resolução é sempre por
    /// <c>system</c> (CPF, CNS, chave local). Quem usa <c>Identifier[0]</c> são os recursos
    /// CLÍNICOS, que não passam por aqui.</para>
    /// </summary>
    public static void UnirIdentifiers(Resource novo, Resource existente)
    {
        var nv = IdentificadoresDe(novo);
        foreach (var id in IdentificadoresDe(existente))
        {
            if (nv.Any(x => x.System == id.System && x.Value == id.Value)) continue;
            if (id.System == SysCpf && !CpfPep.Valido(id.Value)) continue;
            // Conselho fora da forma canônica não é copiado adiante: o build atual já emite a
            // versão normalizada, então manter a antiga deixaria o mesmo CRM duas vezes no
            // recurso, com e sem máscara — e o `&nbsp;` para sempre. Como o descarte só vale
            // quando o novo NÃO traz o valor, registro que sumiu da origem também não persiste.
            if (id.System?.StartsWith(SysConselhoPrefixo, StringComparison.Ordinal) == true
                && !ConselhoPep.Valido(id.Value)) continue;
            nv.Add(id);
        }

        var ordenados = nv
            .OrderBy(x => x.System, StringComparer.Ordinal)
            .ThenBy(x => x.Value, StringComparer.Ordinal)
            .ToList();
        nv.Clear();
        nv.AddRange(ordenados);
    }

    /// <summary>
    /// O upsert é PUT replace-all: o que o build da base atual não traz, some do recurso — e
    /// entre BASES isso apaga fato clínico. O caso que a auditoria cravou: paciente morre no
    /// HMCML (Salux grava <c>deceased</c>); semanas depois o cadastro dele muda no Klinikos, o
    /// ciclo re-upserta pelo CPF, o build do Klinikos não tem óbito — e o morto "revive" no
    /// hub. Fato clínico registrado por uma base só é REMOVIDO por decisão explícita, nunca
    /// por omissão de outra base.
    /// </summary>
    private static void PreservarClinicoEntreBases(Patient novo, Patient atual)
    {
        if (novo.Deceased is null && atual.Deceased is not null)
            novo.Deceased = atual.Deceased;
        if (string.IsNullOrWhiteSpace(novo.BirthDate) && !string.IsNullOrWhiteSpace(atual.BirthDate))
            novo.BirthDate = atual.BirthDate;
        if (novo.Gender is null or AdministrativeGender.Unknown
            && atual.Gender is not null and not AdministrativeGender.Unknown)
            novo.Gender = atual.Gender;
    }
}
