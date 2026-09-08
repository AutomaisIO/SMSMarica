namespace SMSMais.Core.Integracoes.Cadastro;

/// <summary>O que a ficha devolvida pelo CADSUS é, em relação ao que foi perguntado.</summary>
public enum VeredictoCadastro
{
    /// <summary>A fonte devolveu a ficha do identificador perguntado.</summary>
    Confere,

    /// <summary>
    /// Perguntamos por um CNS e voltou outro. <b>É o caso normal</b>, não um erro: o cidadão
    /// acumula números — o provisório (faixa 898) vira definitivo quando o cadastro se regulariza,
    /// e cadastros feitos em lugares diferentes geram outros.
    /// </summary>
    OutroCns,

    /// <summary>
    /// Perguntamos por um CPF e voltou outro. <b>É anormal e perigoso</b>: pode ser a ficha de
    /// outra pessoa. Nunca deve completar cadastro sozinho.
    /// </summary>
    TrocaDeIdentidade,
}

/// <summary>
/// Confere se a ficha que o CADSUS devolveu é mesmo de quem perguntamos.
///
/// <para><b>Por que existe.</b> Na implantação do histórico do SISREG (07–08/09/2026) o SER, uma
/// fonte do CADSUS, devolveu ficha com identificador diferente do perguntado em 595 consultas. O
/// código não conferia — usava o que voltasse. Dois casos foram confirmados na Receita Federal como
/// <b>pessoas diferentes</b>: CPF válido perguntado, ficha de outro cidadão devolvida. Sem esta
/// guarda, um deles completaria o cadastro de um paciente com o nome, o nascimento e a mãe de
/// outro — e ninguém saberia dizer quando aconteceu.</para>
///
/// <para><b>Por que a régua é assimétrica.</b> Medido nas mesmas consultas:</para>
/// <list type="bullet">
///   <item><description><b>CNS:</b> 585 divergências, das quais 577 (98,6%) eram a mesma pessoa com
///   mais de um número. Bloquear aqui quebraria o caminho normal e é justamente o que a coluna
///   <c>cns_todos</c> do hub passou a resolver — o identificador antigo continua valendo.</description></item>
///   <item><description><b>CPF:</b> 10 divergências em 23.781 consultas (0,04%). Oito traziam CPF
///   com dígito verificador inválido — número que não pertence a ninguém; duas eram outra pessoa.
///   <b>Nenhuma era legítima.</b> Aqui bloquear não custa caso bom nenhum.</description></item>
/// </list>
///
/// <para><b>O que não é divergência.</b> Campo vazio ou de preenchimento (<c>000.000.000-00</c>,
/// <c>00000000000</c>) é ficha incompleta, não troca de pessoa — tratar como divergência
/// descartaria cadastro bom. Foi o primeiro falso positivo que apareceu na implantação.</para>
///
/// <para>Estático e sem dependência: a decisão é a parte que precisa de teste, e teste que exige
/// banco ou rede é teste que não roda.</para>
/// </summary>
public static class GuardaIdentidadeCadastro
{
    /// <summary>
    /// Confere a ficha contra a chave perguntada. <paramref name="porCpf"/> diz qual campo comparar
    /// — comparar o campo errado é o defeito silencioso desta rotina: na implantação, comparar CPF
    /// numa consulta feita por CNS marcou como divergente tudo que a fase inteira trouxe.
    /// </summary>
    public static VeredictoCadastro Conferir(string chave, string? devolvido, bool porCpf)
    {
        var pedido = SoDigitos(chave);
        var voltou = SoDigitos(devolvido);

        // Sem um dos lados não há o que comparar. Ficha sem o campo preenchido é ficha incompleta.
        if (pedido.Length == 0 || voltou.Length == 0 || EhPreenchimento(voltou))
            return VeredictoCadastro.Confere;

        if (pedido == voltou)
            return VeredictoCadastro.Confere;

        return porCpf ? VeredictoCadastro.TrocaDeIdentidade : VeredictoCadastro.OutroCns;
    }

    /// <summary>
    /// Só dígitos. As fontes alternam formato no mesmo campo (<c>799.361.217-95</c> e
    /// <c>79936121795</c>): comparar como texto cru acusaria divergência em toda ficha formatada.
    /// </summary>
    private static string SoDigitos(string? valor) =>
        string.IsNullOrEmpty(valor)
            ? string.Empty
            : string.Concat(valor.Where(char.IsAsciiDigit));

    /// <summary>Repetição do mesmo dígito é campo de preenchimento, não identificador.</summary>
    private static bool EhPreenchimento(string digitos) =>
        digitos.All(c => c == digitos[0]);
}
