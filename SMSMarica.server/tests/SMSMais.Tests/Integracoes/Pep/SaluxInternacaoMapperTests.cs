using Hl7.Fhir.Model;
using SMSMarica.Core.Integracoes.Pep.Estrategias.Salux;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// FIA → Encounter class=IMP (ADR-0025), com rigor R4: status do ValueSet encounter-status
/// (não existe "discharged" em R4), class v3-ActCode, dischargeDisposition=exp só com óbito,
/// priority EL/UR pelo CARÁTER SUS (nunca por FIA.ID_INTERNACAO) e Location por partOf.
/// </summary>
public class SaluxInternacaoMapperTests
{
    private static readonly DateTime Agora = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private readonly SaluxFhirMapper _mapper = new("salux-hcml", "https://smsmarica.saude.marica/source/salux/salux-hcml");

    private static FiaLinha Fia(string? dtAlta = null, string? dtBaixa = "2026-07-20T10:00:00",
        string? nrObito = null, string? carater = "2", string? cid = null) =>
        new(CdPaciente: 123, H: 1, Ano: 2026, Nr: 555,
            DtBaixa: dtBaixa, DtAlta: dtAlta, DtAltaMedica: null, DtPrevisaoAlta: "2026-07-28T00:00:00",
            Cid: cid, CidDs: cid is null ? null : "Descrição do CID", NrObito: nrObito,
            Carater: carater, CaraterDs: "Urgência");

    [Fact]
    public void Internacao_em_curso_vira_in_progress_class_imp()
    {
        var enc = _mapper.BuildEncounterInternacao(Fia(), "Patient/abc", null, null, Agora);

        Assert.Equal(Encounter.EncounterStatus.InProgress, enc.Status);
        Assert.Equal("IMP", enc.Class.Code);
        Assert.Equal("http://terminology.hl7.org/CodeSystem/v3-ActCode", enc.Class.System);
        Assert.Contains(enc.Identifier, i => i.System == "urn:salux:fia" && i.Value == "salux-hcml:1-2026-555");
        Assert.Equal("2026-07-20T10:00:00-03:00", enc.Period!.Start);
        Assert.Null(enc.Period.End);
        Assert.Null(enc.Hospitalization); // sem óbito não inventamos motivo de alta
    }

    [Fact]
    public void Alta_vira_finished_com_period_end()
    {
        var enc = _mapper.BuildEncounterInternacao(Fia(dtAlta: "2026-07-25T09:00:00"), "Patient/abc", null, null, Agora);

        Assert.Equal(Encounter.EncounterStatus.Finished, enc.Status);
        Assert.Equal("2026-07-25T09:00:00-03:00", enc.Period!.End);
    }

    [Fact]
    public void Zumbi_sem_alta_ha_mais_de_120_dias_vira_unknown()
    {
        var enc = _mapper.BuildEncounterInternacao(Fia(dtBaixa: "2025-12-01T08:00:00"), "Patient/abc", null, null, Agora);
        Assert.Equal(Encounter.EncounterStatus.Unknown, enc.Status);
    }

    [Fact]
    public void Obito_vira_discharge_disposition_exp()
    {
        var enc = _mapper.BuildEncounterInternacao(
            Fia(dtAlta: "2026-07-25T09:00:00", nrObito: "42"), "Patient/abc", null, null, Agora);

        var disp = enc.Hospitalization!.DischargeDisposition!.Coding[0];
        Assert.Equal("exp", disp.Code);
        Assert.Equal("http://terminology.hl7.org/CodeSystem/discharge-disposition", disp.System);
    }

    [Theory]
    [InlineData("1", "EL")]
    [InlineData("11", "EL")]
    [InlineData("2", "UR")]
    [InlineData("20", "UR")]
    [InlineData("41", null)] // fora do mapeável — priority omitida, nunca inventada
    [InlineData(null, null)]
    public void Carater_sus_mapeia_priority(string? carater, string? esperado)
    {
        var enc = _mapper.BuildEncounterInternacao(Fia(carater: carater), "Patient/abc", null, null, Agora);
        if (esperado is null)
            Assert.Null(enc.Priority);
        else
            Assert.Equal(esperado, enc.Priority!.Coding[0].Code);
    }

    [Fact]
    public void Leito_atual_entra_como_location_ativa_enquanto_internado()
    {
        var leito = new FiaLeitoLinha(1, 2026, 555, 7, "2", "B", "2026-07-21T14:00:00", null);
        var enc = _mapper.BuildEncounterInternacao(Fia(), "Patient/abc", "Location/leito-1", leito, Agora);

        var loc = Assert.Single(enc.Location);
        Assert.Equal("Location/leito-1", loc.Location.Reference);
        Assert.Equal(Encounter.EncounterLocationStatus.Active, loc.Status);
        Assert.Equal("2026-07-21T14:00:00-03:00", loc.Period!.Start);
    }

    [Fact]
    public void Condition_da_fia_carrega_cid_limpo_e_identifier_derivado()
    {
        var cond = _mapper.BuildConditionFia(Fia(cid: "I21 *"), "Patient/abc", "Encounter/e1");

        Assert.NotNull(cond);
        Assert.Equal("I21", cond.Code!.Coding[0].Code);
        Assert.Equal("Encounter/e1", cond.Encounter!.Reference);
        Assert.Contains(cond.Identifier, i => i.System == "urn:salux:fia" && i.Value == "salux-hcml:1-2026-555:cond");
    }

    [Fact]
    public void Sem_cid_nao_gera_condition()
        => Assert.Null(_mapper.BuildConditionFia(Fia(), "Patient/abc", "Encounter/e1"));

    [Fact]
    public void Hierarquia_de_location_setor_quarto_leito()
    {
        var setor = _mapper.BuildLocationSetor(new UnidadeLinha(1, 7, "CLINICA MEDICA", "A"));
        Assert.Equal("wa", setor.PhysicalType!.Coding[0].Code);
        Assert.Equal(Location.LocationStatus.Active, setor.Status);
        Assert.Contains(setor.Identifier, i => i.System == "urn:salux:unidade" && i.Value == "salux-hcml:1-7");

        var quarto = _mapper.BuildLocationQuarto(new QuartoLinha(1, 7, "2", "N", "F"), "Location/setor-1");
        Assert.Equal("ro", quarto.PhysicalType!.Coding[0].Code);
        Assert.Equal("Location/setor-1", quarto.PartOf!.Reference);

        var leito = _mapper.BuildLocationLeito(new LeitoLinha(1, 7, "2", "B", "I", "A", "L"), "Location/quarto-1");
        Assert.Equal("bd", leito.PhysicalType!.Coding[0].Code);
        Assert.Equal(Location.LocationStatus.Active, leito.Status);
    }

    [Theory]
    [InlineData("A", "F", "suspended")]  // bloqueado
    [InlineData("I", "L", "inactive")]   // desativado
    [InlineData("A", "O", "active")]     // ocupado é foto operacional — status segue active
    public void Estado_do_leito_mapeia_status_cadastral(string condicao, string sit, string esperado)
    {
        var leito = _mapper.BuildLocationLeito(new LeitoLinha(1, 7, "2", "B", "I", condicao, sit), null);
        Assert.Equal(esperado, leito.Status!.Value.ToString().ToLowerInvariant());
    }
}
