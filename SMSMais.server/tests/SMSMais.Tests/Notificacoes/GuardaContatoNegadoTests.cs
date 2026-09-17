using Hl7.Fhir.Model;
using SMSMais.Core.Notificacoes.VerificacaoCadastral;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Regras de LGPD do contato (17/09/2026): o vínculo declarado é o que permite o celular da mãe
/// atender pelos filhos, e é ele que decide quando um número ainda pode ser recusado por já ser
/// "de outra pessoa". Testes puros, sem banco.
/// </summary>
public class GuardaContatoNegadoTests
{
    private static ContactPoint Fone(string v) =>
        new() { System = ContactPoint.ContactPointSystem.Phone, Value = v };

    [Fact]
    public void Vinculo_declarado_fica_gravado_no_contato_confirmado()
    {
        var p = new Patient { Telecom = [Fone("21999990000")] };

        PatientMergeFhir.MarcarTelefoneConfirmado(
            p, "5521999990000", DateTimeOffset.UtcNow, VinculoContatoVerificado.MaeOuPaiOuResponsavel);

        Assert.Equal(VinculoContatoVerificado.MaeOuPaiOuResponsavel, PatientMergeFhir.VinculoContatoConfirmado(p));
    }

    [Fact]
    public void Sem_vinculo_declarado_vale_o_proprio_paciente()
    {
        var p = new Patient { Telecom = [Fone("21999990000")] };
        PatientMergeFhir.MarcarTelefoneConfirmado(p, "5521999990000", DateTimeOffset.UtcNow);

        Assert.Equal(VinculoContatoVerificado.Proprio, PatientMergeFhir.VinculoContatoConfirmado(p));
    }

    [Theory]
    [InlineData("sou eu", VinculoContatoVerificado.Proprio)]
    [InlineData("Sou o paciente", VinculoContatoVerificado.Proprio)]
    [InlineData("sou a mãe dele", VinculoContatoVerificado.MaeOuPaiOuResponsavel)]
    [InlineData("sou o responsável", VinculoContatoVerificado.MaeOuPaiOuResponsavel)]
    [InlineData("sou filha dela", VinculoContatoVerificado.OutroParenteOuCuidador)]
    [InlineData("sou a cuidadora", VinculoContatoVerificado.OutroParenteOuCuidador)]
    public void Le_o_vinculo_em_texto_livre(string texto, VinculoContatoVerificado esperado) =>
        Assert.Equal(esperado, InterpretadorRespostaCidadao.TentarLerVinculo(texto));

    [Theory]
    [InlineData("bom dia")]
    [InlineData("não entendi")]
    public void Resposta_que_nao_diz_o_vinculo_devolve_nulo(string texto) =>
        Assert.Null(InterpretadorRespostaCidadao.TentarLerVinculo(texto));
}
