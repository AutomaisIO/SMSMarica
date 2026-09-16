using SMSMais.Core.Regulacao.FollowUp;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Tests.Regulacao.FollowUp;

/// <summary>
/// O verbo da trilha do SER/SERNIT vira enum UMA vez, na captura. A regra tem de ser tão tolerante
/// quanto o sincronizador original era (caixa, hífen, espaço), e a mesma para os dois sistemas.
/// </summary>
public class ClassificadorEventoRegulacaoTests
{
    [Theory]
    [InlineData("FollowUP", TipoEventoExterno.FollowUp)]
    [InlineData("followup", TipoEventoExterno.FollowUp)]
    [InlineData("Follow-UP", TipoEventoExterno.FollowUp)]
    [InlineData("Follow up", TipoEventoExterno.FollowUp)]
    [InlineData("Solicitar", TipoEventoExterno.Solicitar)]
    [InlineData("SOLICITAR", TipoEventoExterno.Solicitar)]
    [InlineData("Pendenciar", TipoEventoExterno.Pendenciar)]
    [InlineData("Cancelar", TipoEventoExterno.Cancelar)]
    [InlineData("Agendar", TipoEventoExterno.Agendar)]
    [InlineData("Chegada no Destino", TipoEventoExterno.ChegadaNoDestino)]
    [InlineData("Transferir", TipoEventoExterno.Transferir)]
    [InlineData("Devolvido para a regulação", TipoEventoExterno.DevolvidoParaRegulacao)]
    [InlineData("WhatsApp", TipoEventoExterno.WhatsApp)]
    [InlineData("Reagendar", TipoEventoExterno.Reagendar)]
    [InlineData("Retornar para Fila", TipoEventoExterno.RetornarParaFila)]
    [InlineData("Retornar para Fila, não apto", TipoEventoExterno.RetornarParaFila)]
    [InlineData("Alta", TipoEventoExterno.Alta)]
    [InlineData("Dar Alta", TipoEventoExterno.Alta)]
    [InlineData("Corrigir dados da solicitação", TipoEventoExterno.CorrigirDados)]
    [InlineData("Confirmar presença", TipoEventoExterno.Outro)]
    [InlineData("", TipoEventoExterno.Outro)]
    [InlineData(null, TipoEventoExterno.Outro)]
    public void Verbo_vira_tipo(string? verbo, TipoEventoExterno esperado) =>
        Assert.Equal(esperado, ClassificadorEventoRegulacao.TipoDoVerbo(verbo));

    [Fact]
    public void Hash_das_regras_e_estavel_e_muda_com_o_conteudo()
    {
        var a = ClassificadorEventoRegulacao.HashDasRegras(SementeFollowUp.Json);
        var b = ClassificadorEventoRegulacao.HashDasRegras(SementeFollowUp.Json);
        // Só espaço e quebra de linha: é o que o jsonb faz ao devolver o texto. Mesmo hash.
        var reformatado = ClassificadorEventoRegulacao.HashDasRegras(
            SementeFollowUp.Json.Replace("\n", string.Empty).Replace("  ", string.Empty));
        // Uma regra a menos: conjunto diferente, hash diferente.
        var c = ClassificadorEventoRegulacao.HashDasRegras(
            SementeFollowUp.Json.Replace("\"ordem\": 1,", "\"ordem\": 100,"));

        Assert.Equal(16, a.Length);
        Assert.Equal(a, b);
        Assert.Equal(a, reformatado);
        Assert.NotEqual(a, c);
        // Regras vazias também têm identidade: é o que permite reclassificar quando alguém
        // carrega a semente numa instância que nasceu sem regra nenhuma.
        Assert.Equal(16, ClassificadorEventoRegulacao.HashDasRegras(null).Length);
    }
}
