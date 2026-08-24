using SMSMarica.Core.Conversas;

namespace SMSMais.Tests.Notificacoes;

public class TelefoneWhatsAppTests
{
    [Theory]
    [InlineData("5521999990000", true)]   // canônico completo
    [InlineData("21999990000", true)]     // sem DDI (11 dígitos locais)
    [InlineData("(21) 99999-0000", true)] // formatado
    [InlineData("2199671643", true)]      // celular FORMATO ANTIGO (sem o nono dígito)
    [InlineData("552199671643", true)]    // formato antigo com DDI
    [InlineData("21 8888-7777", true)]    // formato antigo começando em 8 (ainda celular)
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

    [Theory]
    [InlineData("2199671643", "5521999671643")]   // insere o nono dígito (celular antigo)
    [InlineData("21 8888-7777", "5521988887777")] // idem começando em 8
    [InlineData("2133334444", "552133334444")]    // fixo fica como está
    [InlineData("5521999990000", "5521999990000")] // já completo fica como está
    public void NormalizarNonoDigito_completa_celular_antigo(string entrada, string esperado) =>
        Assert.Equal(esperado, TelefoneWhatsApp.NormalizarNonoDigito(entrada));

    [Theory]
    [InlineData("21999990000", "5521999990000")]       // DDD + celular
    [InlineData("(21) 99999-0000", "5521999990000")]   // com máscara
    [InlineData("+55 21 99999-0000", "5521999990000")] // com DDI e +
    [InlineData("021 99999-0000", "5521999990000")]    // prefixo de discagem (0 + DDD)
    [InlineData("0 21 21 99999-0000", "5521999990000")]// operadora (0 + 21) + DDD + número
    [InlineData("99999-0000", "5521999990000")]        // sem DDD → assume 21 (Maricá)
    [InlineData("2199671643", "5521999671643")]        // celular antigo (8 dígitos) ganha o 9
    [InlineData("9967-1643", "5521999671643")]         // sem DDD e sem o nono dígito
    public void Interpretar_aceita_as_variacoes_do_balcao(string entrada, string esperado)
    {
        var r = TelefoneWhatsApp.Interpretar(entrada);
        Assert.True(r.Ok, r.Erro);
        Assert.Equal(esperado, r.Fone);
    }

    [Theory]
    [InlineData("21 3333-4444")]  // fixo
    [InlineData("+1 555 123 4567")] // estrangeiro
    [InlineData("1234")]          // curto demais
    [InlineData("")]
    public void Interpretar_recusa_o_que_nao_e_celular_br(string entrada)
    {
        var r = TelefoneWhatsApp.Interpretar(entrada);
        Assert.False(r.Ok);
        Assert.False(string.IsNullOrWhiteSpace(r.Erro));
    }
}
