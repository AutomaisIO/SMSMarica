using SMSMarica.Core.Conversas;

namespace SMSMarica.Tests.Notificacoes;

public class TelefoneWhatsAppTests
{
    [Theory]
    [InlineData("5521999990000", true)]   // canônico completo
    [InlineData("21999990000", true)]     // sem DDI (11 dígitos locais)
    [InlineData("(21) 99999-0000", true)] // formatado
    [InlineData("2133334444", false)]     // fixo (10 dígitos)
    [InlineData("552133334444", false)]   // fixo com DDI
    [InlineData("5521899990000", false)]  // 9º dígito não é 9
    [InlineData("5501999990000", false)]  // DDD inválido (01)
    [InlineData("15551234567", false)]    // estrangeiro (EUA)
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EhCelularBr_classifica_corretamente(string? telefone, bool esperado) =>
        Assert.Equal(esperado, TelefoneWhatsApp.EhCelularBr(telefone));

    [Theory]
    [InlineData("(21) 99999-0000", "5521999990000")]
    [InlineData("5521999990000", "5521999990000")]
    [InlineData("21 3333-4444", "552133334444")]
    public void Canonizar_normaliza(string entrada, string esperado) =>
        Assert.Equal(esperado, TelefoneWhatsApp.Canonizar(entrada));
}
