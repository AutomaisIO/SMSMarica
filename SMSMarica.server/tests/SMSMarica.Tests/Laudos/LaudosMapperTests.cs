using SMSMarica.Core.Laudos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Tests.Laudos;

/// <summary>
/// Blindagem do mapeamento POSICIONAL de <see cref="Laudo"/> → DTOs. Os DTOs são
/// records posicionais: um argumento fora de ordem no <see cref="LaudosMapper"/>
/// troca valores em silêncio (compila e passa no build). Estes testes fixam cada
/// campo com valor distinto e conferem que caiu no lugar certo — em especial o
/// PacienteNomeDicom recém-inserido e seus vizinhos (PacienteId, MedicoId, nomes).
/// </summary>
public class LaudosMapperTests
{
    private static Laudo Exemplo() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        StudyInstanceUID = "2.25.42",
        Versao = 7,
        LaudoAnteriorId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        PacienteId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        PacienteNomeDicom = "MARIA DA SILVA",
        MedicoId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        MedicoNomeSnapshot = "DRA HOUSE",
        MedicoCrmSnapshot = "12345",
        MedicoUfCrmSnapshot = "RJ",
        MedicoRqeSnapshot = "999",
        LaudoTemplateId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
        Titulo = "Mamografia bilateral",
        ConteudoJson = """{"x":1}""",
        ConteudoHtml = "<p>laudo</p>",
        Status = StatusLaudo.Finalizado,
        BiRads = "4A",
        BiRadsSugerido = "4B",
        RespostasChecklist = """{"a":true}""",
        FinalizadoEm = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc),
        CriadoEm = new DateTime(2026, 6, 30, 9, 0, 0, DateTimeKind.Utc),
        AtualizadoEm = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void ParaListItem_mapeia_cada_campo_na_posicao_certa()
    {
        var l = Exemplo();
        var dto = LaudosMapper.ParaListItem(l);

        dto.Id.Should().Be(l.Id);
        dto.StudyInstanceUID.Should().Be("2.25.42");
        dto.Versao.Should().Be(7);
        dto.PacienteId.Should().Be(l.PacienteId);
        dto.PacienteNome.Should().BeNull(); // resolvido no hub FHIR depois, nunca no mapper
        dto.PacienteNomeDicom.Should().Be("MARIA DA SILVA");
        dto.MedicoId.Should().Be(l.MedicoId);
        dto.MedicoNome.Should().Be("DRA HOUSE");
        dto.Titulo.Should().Be("Mamografia bilateral");
        dto.Status.Should().Be(StatusLaudo.Finalizado);
        dto.BiRads.Should().Be("4A");
        dto.FinalizadoEm.Should().Be(l.FinalizadoEm);
        dto.CriadoEm.Should().Be(l.CriadoEm);
        dto.Assinado.Should().BeFalse();
    }

    [Fact]
    public void ParaDto_mapeia_cada_campo_na_posicao_certa()
    {
        var l = Exemplo();
        var dto = LaudosMapper.ParaDto(l);

        dto.Id.Should().Be(l.Id);
        dto.StudyInstanceUID.Should().Be("2.25.42");
        dto.Versao.Should().Be(7);
        dto.LaudoAnteriorId.Should().Be(l.LaudoAnteriorId);
        dto.PacienteId.Should().Be(l.PacienteId);
        dto.PacienteNome.Should().BeNull();
        dto.PacienteCpf.Should().BeNull();
        dto.PacienteNomeDicom.Should().Be("MARIA DA SILVA");
        dto.MedicoId.Should().Be(l.MedicoId);
        dto.MedicoNome.Should().Be("DRA HOUSE");
        dto.MedicoCrm.Should().Be("12345");
        dto.MedicoUfCrm.Should().Be("RJ");
        dto.MedicoRqe.Should().Be("999");
        dto.LaudoTemplateId.Should().Be(l.LaudoTemplateId);
        dto.LaudoTemplateNome.Should().BeNull(); // navegação não carregada
        dto.Titulo.Should().Be("Mamografia bilateral");
        dto.Status.Should().Be(StatusLaudo.Finalizado);
        dto.BiRads.Should().Be("4A");
        dto.BiRadsSugerido.Should().Be("4B");
        dto.CriadoEm.Should().Be(l.CriadoEm);
    }

    [Fact]
    public void ParaListItem_sem_nome_dicom_fica_null()
    {
        var l = Exemplo();
        l.PacienteNomeDicom = null;
        LaudosMapper.ParaListItem(l).PacienteNomeDicom.Should().BeNull();
    }
}
