using SMSMarica.Core.Telefones;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Telefones;

/// <summary>
/// Régua da dispensa de verificação de contato. Sem banco de propósito: é a decisão de
/// "este motivo deixa dado clínico sair por WhatsApp?" que precisa estar travada — errar para
/// o lado permissivo manda exame e laudo para um número que ninguém confirmou.
/// </summary>
public class DispensaContatoRegrasTests
{
    [Theory]
    // Existe um número utilizável e consentido → resultado/laudo continuam saindo por WhatsApp.
    [InlineData(MotivoDispensaContato.NumeroDeTerceiro, true)]
    [InlineData(MotivoDispensaContato.NaoConsegueConfirmar, true)]
    [InlineData(MotivoDispensaContato.SemSinalNoMomento, true)]
    // Não há para onde mandar (ou o motivo é desconhecido) → entrega presencial.
    [InlineData(MotivoDispensaContato.SemCelular, false)]
    [InlineData(MotivoDispensaContato.SemWhatsApp, false)]
    [InlineData(MotivoDispensaContato.RecusaValidar, false)]
    [InlineData(MotivoDispensaContato.Outro, false)]
    public void Motivo_define_se_dado_clinico_pode_sair_por_whatsapp(
        MotivoDispensaContato motivo, bool esperado) =>
        Assert.Equal(esperado, DispensaContatoRegras.PermiteEnvio(motivo));

    [Fact]
    public void Todo_motivo_do_enum_aparece_na_lista_da_recepcao()
    {
        // Motivo novo no enum que ninguém pôs em Todos vira opção invisível no balcão —
        // a recepção não consegue escolher e o caso volta a travar.
        var doEnum = Enum.GetValues<MotivoDispensaContato>().ToHashSet();
        Assert.Equal(doEnum, DispensaContatoRegras.Todos.ToHashSet());
    }

    [Fact]
    public void Todo_motivo_tem_rotulo_e_consequencia_proprios()
    {
        foreach (var m in DispensaContatoRegras.Todos)
        {
            // Rótulo caindo no ToString() = motivo novo sem texto de tela.
            Assert.NotEqual(m.ToString(), DispensaContatoRegras.Rotulo(m));
            Assert.False(string.IsNullOrWhiteSpace(DispensaContatoRegras.Consequencia(m)));
        }
    }
}
