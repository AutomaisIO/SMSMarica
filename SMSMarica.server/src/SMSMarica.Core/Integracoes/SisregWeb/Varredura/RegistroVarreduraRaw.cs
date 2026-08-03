using System.Text.Json;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura;

/// <summary>
/// O que o SISREG mostrou na agenda, <b>as-is</b>, mais qual busca encontrou o registro. É a
/// proveniência da varredura — o equivalente ao <c>LinhaRaw</c> do TXT.
///
/// <para><b>Tudo string, de propósito:</b> o RAW é observação, não interpretação. Guardar
/// <c>DateTime</c> aqui seria congelar a leitura do parser da época; guardando o texto, um parser
/// corrigido amanhã relê o histórico certo.</para>
///
/// <para><b>Guarda o <c>pa</c>, nunca o SIGTAP.</b> O de-para é <i>configuração</i>, não observação:
/// no reprocesso ele é resolvido de novo. É isso que faz o botão "Validar" funcionar depois que o
/// operador confirma o SIGTAP que faltava — a pendência entra sozinha, sem reimportar nada.</para>
/// </summary>
/// <param name="V">Versão do envelope. Mudou o formato, sobe V e o mapper passa a tratar as duas.</param>
public sealed record RegistroVarreduraRaw(
    int V,
    string CoSolicitacao,
    string? Cns,
    string? Paciente,
    string? Nascimento,
    string? Idade,
    string? Origem,
    string? Telefones,
    string? UnidadeSolicitante,
    string? CnesSolicitante,
    string? VagaSolicitada,
    string? VagaConsumida,
    string? Cid10,
    string? Data,
    string? DiaSemana,
    string? Hora,
    string? Situacao,
    string? Procedimentos,
    string ProfCpf,
    string ProfNome,
    string PaCodigo,
    string PaNome,
    string CnesExecutante,
    string NomeExecutante,
    DateTime CapturadoEm)
{
    /// <summary>Versão corrente do envelope.</summary>
    public const int VersaoAtual = 1;

    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = false };

    public string Serializar() => JsonSerializer.Serialize(this, Opcoes);

    /// <summary>
    /// Lê o envelope de um RAW. Devolve null se o texto não for um envelope válido — o RAW de uma
    /// falha de TXT cai aqui quando a <c>Origem</c> foi carimbada errada, e nesse caso é melhor
    /// degradar para "ilegível" do que estourar no reprocesso do operador.
    /// </summary>
    public static RegistroVarreduraRaw? Desserializar(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            var registro = JsonSerializer.Deserialize<RegistroVarreduraRaw>(raw, Opcoes);
            return string.IsNullOrWhiteSpace(registro?.CoSolicitacao) ? null : registro;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
