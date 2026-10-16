using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Worklist;

/// <summary>
/// Converte uma <see cref="ExameImagem"/> + dados do paciente (hub FHIR) num
/// item de Modality Worklist (MWL) DICOM+JSON, aceito pelo endpoint <c>/mwlitems</c>
/// do dcm4chee. O equipamento (ex.: Fuji FDR-3000AWS) consome via C-FIND MWL
/// clássico, filtrando por ScheduledStationAETitle + Modality + Data — por isso a
/// SPS (0040,0100) carrega esses campos. Formato JSON conforme PS3.18 Annex F.
/// </summary>
internal static class ConstrutorMwlItem
{
    /// <summary>SPS ID (0040,0009) — junto com o StudyInstanceUID forma a chave do MWL
    /// item (usada nos GET/DELETE do dcm4chee). Usa o mesmo valor do RequestedProcedureID
    /// (≤10 chars): o Fuji recusa SH acima de 10 e antes era "SPS-{accession}" (14 chars),
    /// o que quebrava a execução do estudo ("obter informações de imagem").</summary>
    public static string SpsId(ExameImagem s) => RequestedProcedureId(s);

    /// <summary>RequestedProcedureID (0040,1001) limitado a 10 caracteres: apesar de o
    /// DICOM SH permitir 16, o Fuji rejeita IDs maiores. Deriva do AccessionNumber
    /// removendo o prefixo "SMS" — sobra ano+sequência (ex.: SMS2026000001 -> 2026000001,
    /// 10 chars, único por ano). Para accession em formato inesperado, trunca nos
    /// últimos 10 caracteres. Não é chave de lookup (o vínculo é por SpsId + StudyUID).</summary>
    public static string RequestedProcedureId(ExameImagem s)
    {
        var acc = s.AccessionNumber ?? string.Empty;
        var id = acc.StartsWith("SMS", StringComparison.Ordinal) ? acc[3..] : acc;
        return id.Length <= 10 ? id : id[^10..];
    }

    /// <summary>PatientID exibido no equipamento: CPF (só dígitos) quando houver,
    /// senão o id do paciente no hub FHIR (Guid). O vínculo do estudo de volta é pelo
    /// StudyInstanceUID — não por este ID — então usar CPF é seguro e mais legível.</summary>
    public static string PatientId(ExameImagem s, PacienteResumo p)
    {
        var cpf = new string((p.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        return cpf.Length == 11 ? cpf : s.Solicitacao!.PacienteId.ToString();
    }

    /// <summary>IssuerOfPatientID (0010,0021) quando o PatientID é o CPF — a autoridade que
    /// emitiu aquele número.
    /// <para><b>Por que isto existe:</b> o dcm4chee tem uma coerção que, na ausência de issuer,
    /// fabrica um a partir do <b>nome</b> e do nascimento
    /// (<c>DCM4CHEE.{PatientName,hash}.{PatientBirthDate,hash}</c>). Com isso o nome passa a fazer
    /// parte da identidade: corrigir um sobrenome no cadastro criava um SEGUNDO paciente para o
    /// mesmo CPF, e o registro seguinte batia em 409 por ambiguidade. Mandando um issuer estável,
    /// o CPF vira o código único de verdade — o nome pode mudar à vontade.</para>
    /// <para><b>Só para CPF.</b> Paciente sem CPF entra com o Guid do hub e <b>sem</b> issuer: para
    /// esses a coerção antiga continua valendo, e mandar um issuer nosso faria o registro deixar de
    /// casar com a imagem que o equipamento devolve. Ver
    /// <c>docs/pendencias/identidade-do-paciente-no-pacs.md</c>.</para></summary>
    public const string IssuerCpf = "CPF";

    /// <summary>Issuer correspondente a um PatientID, ou <c>null</c> quando não é CPF.</summary>
    public static string? IssuerDoPatientId(string patientId) =>
        patientId.Length == 11 && patientId.All(char.IsAsciiDigit) ? IssuerCpf : null;

    /// <summary>
    /// Item MWL completo (top-level + Scheduled Procedure Step Sequence).
    /// <para>
    /// O <b>WorklistLabel (0074,1202)</b> repete o AE da estação de propósito: um Archive AE do
    /// dcm4chee configurado com <c>dcmMWLWorklistLabel</c> só devolve, no C-FIND MWL, os itens
    /// daquele label — é a trava do lado do SERVIDOR para o equipamento de uma unidade não puxar
    /// a lista de outra, mesmo que o técnico esqueça de filtrar por ScheduledStationAETitle na
    /// máquina. Item SEM label é devolvido a todos os AEs, então carimbar sempre é o que faz a
    /// separação valer. O AE administrativo usado por nós (<c>WORKLIST</c>) não tem label e
    /// continua enxergando tudo — é por ele que criamos, confirmamos e removemos itens.
    /// </para>
    /// </summary>
    public static JsonObject Item(
        ExameImagem s, PacienteResumo paciente, string stationAeTitle,
        int descricaoMax = Equipamento.DescricaoMaxPadrao,
        DestinoCodigoProcedimento codigoDestinos = DestinoCodigoProcedimento.Nenhum,
        string? codigoEsquema = null,
        CamposOpcionaisWorklist? campos = null)
    {
        var tipo = s.TipoExame ?? throw new InvalidOperationException("TipoExame não carregado.");
        var quando = s.Solicitacao!.DataAgendada ?? DateTime.UtcNow;

        var item = new JsonObject
        {
            ["00080050"] = Sh(s.AccessionNumber),                            // AccessionNumber
            ["00100010"] = Pn(paciente.Nome),                               // PatientName
            ["00100020"] = Lo(PatientId(s, paciente)),                      // PatientID (CPF ou Guid do hub FHIR)
            ["00100030"] = Da(paciente.DataNascimento),                     // PatientBirthDate
            // 00100021 (IssuerOfPatientID) entra logo abaixo, só quando o PatientID é o CPF.
            ["00100040"] = Cs(MapearSexo(paciente.Sexo)),                   // PatientSex
            ["0020000D"] = Ui(s.StudyInstanceUID),                          // StudyInstanceUID
            ["00321060"] = LoDesc(tipo.RequestedProcedureDescription, descricaoMax),  // RequestedProcedureDescription
            ["00401001"] = Sh(RequestedProcedureId(s)),                     // RequestedProcedureID (<=10; se omitido o dcm4chee gera RP-XXXXXXXX >10)
            ["00401003"] = Sh(MapearPrioridade(s.Solicitacao!.Prioridade)),             // RequestedProcedurePriority
            ["00741202"] = Lo(stationAeTitle),                              // WorklistLabel — ver nota abaixo
            ["00400100"] = new JsonObject                                   // ScheduledProcedureStepSequence
            {
                ["vr"] = "SQ",
                ["Value"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["00080060"] = Cs(tipo.ModalidadeDicom.ToString()),         // Modality
                        ["00400001"] = Ae(stationAeTitle),                          // ScheduledStationAETitle (filtro do equipamento)
                        ["00400002"] = ComVr("DA", quando.ToString("yyyyMMdd")),    // SPS StartDate
                        ["00400003"] = ComVr("TM", quando.ToString("HHmmss")),      // SPS StartTime
                        ["00400007"] = LoDesc(tipo.ScheduledProcedureStepDescription, descricaoMax), // SPS Description
                        ["00400009"] = Sh(SpsId(s)),                                // SPS ID (<=10; se omitido o dcm4chee gera SPS-XXXXXXXX >10)
                        ["00400010"] = Sh(stationAeTitle),                          // ScheduledStationName
                        ["00400020"] = Cs("SCHEDULED"),                             // SPS Status
                    },
                },
            },
        }.ComIssuer(PatientId(s, paciente));

        // Código do procedimento — SÓ para o aparelho que pediu (ADR-0074). Desligado, o item sai
        // exatamente como antes: nada de sequência vazia, nada de campo novo.
        if (campos?.CodigoProcedimentoTag is { } tagCodigo)
        {
            // Aparelho que lê o código num campo de texto, fora de sequência: vai só o código.
            if (CodigoDoProcedimento(s, descricaoMax, codigoEsquema) is { } codigo)
                Texto(item, tagCodigo, codigo.Valor);
        }
        else if (codigoDestinos != DestinoCodigoProcedimento.Nenhum
            && CodigoDoProcedimento(s, descricaoMax, codigoEsquema) is { } codigo)
        {
            if (codigoDestinos.HasFlag(DestinoCodigoProcedimento.ProcedimentoSolicitado))
                item["00321064"] = SequenciaDeCodigo(codigo);              // RequestedProcedureCodeSequence
            if (codigoDestinos.HasFlag(DestinoCodigoProcedimento.ProtocoloAgendado))
                Sps(item)["00400008"] = SequenciaDeCodigo(codigo);         // ScheduledProtocolCodeSequence
        }

        // Médico e unidade (adendo do ADR-0074): também só no aparelho que ligou, na tag que ele lê.
        if (campos?.MedicoSolicitanteTag is { } tagMedico)
            Texto(item, tagMedico, s.Solicitacao!.SolicitanteNome);
        if (campos?.UnidadeTag is { } tagUnidade)
            Texto(item, tagUnidade, NomeDaUnidade(s.Solicitacao!, campos.UnidadeOrigem));

        return item;
    }

    /// <summary>A etapa agendada (0040,0100) do item — onde vão os atributos do módulo SPS.</summary>
    private static JsonObject Sps(JsonObject item) => (JsonObject)((JsonArray)item["00400100"]!["Value"]!)[0]!;

    /// <summary>
    /// Nome da unidade escolhida como origem. Precisa da navegação carregada — quem monta o item
    /// inclui as duas unidades da solicitação; sem ela, o campo simplesmente não vai.
    /// </summary>
    private static string? NomeDaUnidade(Solicitacao s, OrigemUnidadeWorklist origem) =>
        origem == OrigemUnidadeWorklist.Solicitante ? s.UnidadeSolicitante?.Nome : s.UnidadeExecutante?.Nome;

    /// <summary>
    /// Texto numa tag escolhida no cadastro do equipamento, no VR que o dicionário DICOM dá a ela,
    /// sem acento e no teto do VR. Sem valor, a tag não entra — campo vazio no aparelho é melhor que
    /// um "null". Tag que deixou de ser utilizável (cadastro anterior a uma regra) também não entra:
    /// o item segue válido e o resto chega.
    /// </summary>
    private static void Texto(JsonObject item, string tag, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || !TagDicomWorklist.Utilizavel(tag)) return;
        var vr = TagDicomWorklist.Vr(tag)!;
        var destino = TagDicomWorklist.VaiNaEtapaAgendada(tag) ? Sps(item) : item;
        destino[tag] = vr == "PN"
            ? new JsonObject
            {
                ["vr"] = "PN",
                ["Value"] = new JsonArray(new JsonObject { ["Alphabetic"] = Truncar(FormatarPn(valor), 64) }),
            }
            : ComVr(vr, Truncar(TextoLimpo(valor), TagDicomWorklist.Teto(vr)));
    }

    /// <summary>Sem acento, sem barra invertida (separador de múltiplos valores em LO/SH) e com os
    /// espaços colapsados.</summary>
    private static string TextoLimpo(string v) =>
        string.Join(' ', Ascii(v).Replace('\\', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// O item de código do procedimento: código interno do SISREG em (0008,0100), esquema em
    /// (0008,0102), descrição em (0008,0104). O código é o do procedimento que ESTE exame executa —
    /// no pedido com joelho D e E, cada exame leva o seu (<see cref="Solicitacoes.ExamesDoPedido"/>). Sem código no
    /// pedido (lançado à mão), vale o do tipo de exame; sem nenhum, não há item — e o aparelho
    /// fica como hoje, com o técnico escolhendo o protocolo.
    /// </summary>
    internal static CodigoProcedimento? CodigoDoProcedimento(ExameImagem s, int descricaoMax, string? esquema)
    {
        var procedimento = Solicitacoes.ExamesDoPedido.Procedimento(s);
        var codigo = SoDigitos(procedimento.CodigoSisreg);
        if (codigo.Length == 0) codigo = SoDigitos(s.TipoExame?.CodigoSisreg);
        if (codigo.Length == 0) return null;

        var texto = procedimento.Texto ?? s.TipoExame?.Nome ?? string.Empty;
        return new CodigoProcedimento(
            codigo,
            string.IsNullOrWhiteSpace(esquema) ? Equipamento.CodigoProcedimentoEsquemaPadrao : esquema.Trim(),
            DescricaoDoCodigo(texto, descricaoMax));
    }

    /// <summary>
    /// (0008,0104) sem acento e no tamanho que o aparelho aguenta. Abrevia ANTES de cortar: dois
    /// nomes do SISREG passam de 64 ("...(AP + LATERAL + OBLIQUA + 3 AXIAIS) DIREITO") e, cortados,
    /// o direito e o esquerdo ficariam iguais.
    /// </summary>
    internal static string DescricaoDoCodigo(string texto, int max)
    {
        var t = string.Join(' ', Ascii(texto).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        foreach (var (de, para) in new[] { ("RADIOGRAFIA", "RX"), (" + ", "+"), ("( ", "(") })
        {
            if (t.Length <= max) break;
            t = t.Replace(de, para, StringComparison.Ordinal);
        }
        return Truncar(t, max);
    }

    private static JsonObject SequenciaDeCodigo(CodigoProcedimento c) => new()
    {
        ["vr"] = "SQ",
        ["Value"] = new JsonArray
        {
            new JsonObject
            {
                ["00080100"] = Sh(c.Valor),      // CodeValue — o código interno do SISREG
                ["00080102"] = Sh(c.Esquema),    // CodingSchemeDesignator
                ["00080104"] = Lo(c.Descricao),  // CodeMeaning
            },
        },
    };

    private static string SoDigitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsAsciiDigit)]);

    internal sealed record CodigoProcedimento(string Valor, string Esquema, string Descricao);

    /// <summary>Recurso Patient mínimo para registrar/atualizar no dcm4chee antes do
    /// MWL item (o <c>POST /mwlitems</c> exige que o paciente já exista no arquivo).</summary>
    public static JsonObject Paciente(ExameImagem s, PacienteResumo p) => new JsonObject
    {
        ["00100010"] = Pn(p.Nome),
        ["00100020"] = Lo(PatientId(s, p)),
        ["00100030"] = Da(p.DataNascimento),
        ["00100040"] = Cs(MapearSexo(p.Sexo)),
    }.ComIssuer(PatientId(s, p));

    /// <summary>Acrescenta IssuerOfPatientID (0010,0021) quando o PatientID é o CPF. Devolve o
    /// mesmo objeto para encadear.</summary>
    private static JsonObject ComIssuer(this JsonObject ds, string patientId)
    {
        if (IssuerDoPatientId(patientId) is { } issuer) ds["00100021"] = Lo(issuer);
        return ds;
    }

    // ---- helpers DICOM+JSON ----

    private static JsonObject ComVr(string vr, string v) => new() { ["vr"] = vr, ["Value"] = new JsonArray(v) };
    private static JsonObject Sh(string v) => ComVr("SH", Ascii(v));
    private static JsonObject Lo(string v) => ComVr("LO", Ascii(v));

    /// <summary>
    /// Descrição (VR <c>LO</c>) cortada no que <b>aquele aparelho</b> aguenta — o padrão é 64, o
    /// teto do próprio DICOM.
    /// <para>O corte é configurável por equipamento
    /// (<see cref="Equipamento.DescricaoMaxCaracteres"/>) porque um único console impõe menos: o
    /// Fuji FDR-3000AWS falha ao montar a imagem ("obter informações de imagem", erro 31027) com
    /// descrição longa, e a worklist de referência que ele aceitava usava ~10 ("Mamografia"). Até
    /// 09/09/2026 esse 16 era fixo e valia para todos, truncando 189 dos 205 tipos de exame no
    /// meio da palavra.</para></summary>
    private static JsonObject LoDesc(string v, int max) => Lo(Truncar(v, max));

    private static string Truncar(string? v, int max)
    {
        v = (v ?? string.Empty).Trim();
        return v.Length <= max ? v : v[..max].TrimEnd();
    }
    private static JsonObject Ui(string v) => ComVr("UI", v);
    private static JsonObject Cs(string v) => ComVr("CS", v);
    private static JsonObject Ae(string v) => ComVr("AE", v);
    private static JsonObject Pn(string nome) => new() { ["vr"] = "PN", ["Value"] = new JsonArray(new JsonObject { ["Alphabetic"] = FormatarPn(nome) }) };

    /// <summary>Remove acentos/diacríticos e qualquer caractere não-ASCII. O equipamento
    /// Fuji não aceita acentos no MWL e, como não declaramos SpecificCharacterSet (item
    /// fica em ASCII puro / ISO_IR 6), caracteres acentuados chegavam corrompidos (viravam
    /// '?'). Ex.: "diagnóstica" -> "diagnostica", "AVALIAÇÃO" -> "AVALIACAO".</summary>
    private static string Ascii(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? string.Empty;
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // diacrítico
            if (c <= 0x7F) sb.Append(c); // mantém só ASCII; resíduo não-ASCII é descartado
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
    private static JsonObject Da(DateOnly? d) => d is null ? new JsonObject { ["vr"] = "DA" } : ComVr("DA", d.Value.ToString("yyyyMMdd"));

    private static string MapearSexo(Sexo sexo) => sexo switch
    {
        Sexo.Masculino => "M",
        Sexo.Feminino => "F",
        _ => "O",
    };

    private static string MapearPrioridade(PrioridadeSolicitacao p) => p switch
    {
        PrioridadeSolicitacao.Urgente => "STAT",
        PrioridadeSolicitacao.Prioritaria => "HIGH",
        _ => "ROUTINE",
    };

    /// <summary>"João da Silva" → "SILVA^JOAO DA" (DICOM PN: Family^Given, sem acentos).
    /// <para><b>Público de propósito:</b> a correção de identidade reescreve o PatientName dentro
    /// do objeto DICOM e precisa usar EXATAMENTE esta régua. Se cada lado formatasse do seu jeito,
    /// um estudo corrigido e um estudo novo do mesmo paciente ficariam com nomes diferentes no
    /// PACS — e a busca por nome passaria a achar um e não o outro.</para></summary>
    public static string FormatarPn(string nome)
    {
        var n = Ascii(nome).Trim();
        if (string.IsNullOrEmpty(n)) return "PACIENTE";
        var partes = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 1) return partes[0].ToUpperInvariant();
        var ultimo = partes[^1].ToUpperInvariant();
        var primeiros = string.Join(' ', partes[..^1]).ToUpperInvariant();
        return $"{ultimo}^{primeiros}";
    }
}
