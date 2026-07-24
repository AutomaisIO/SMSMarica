namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Aba da planilha contratual de indicadores do HMCML. O valor inteiro é persistido —
/// não renumerar.
/// </summary>
public enum AbaIndicador
{
    Adulto = 1,
    Pediatrico = 2,
    MaternoInfantil = 3,
    PerfilEpidemiologico = 4,
    Institucional = 5,
}

/// <summary>
/// Como o resultado sai do numerador e do denominador. Espelha as fórmulas da planilha
/// contratual — lá <b>todo</b> indicador é <c>=IFERROR(numerador/denominador,"-")</c>,
/// com multiplicador só na densidade. A unidade (%/min/dias) é formatação, não cálculo.
/// </summary>
public enum TipoResultadoIndicador
{
    /// <summary>
    /// numerador ÷ denominador. Cobre tanto "tempo médio" (Σ minutos ÷ pacientes) quanto
    /// percentual (a planilha guarda fração e compara com <c>30%</c> = 0,30).
    /// </summary>
    Razao = 1,

    /// <summary>(numerador ÷ denominador) × <see cref="Indicador.FatorDensidade"/>. Ex.: óbitos por 1.000 internações.</summary>
    Densidade = 2,

    /// <summary>Contagem simples — o numerador é o resultado. Ex.: número de óbitos maternos.</summary>
    Absoluto = 3,

    /// <summary>N linhas de distribuição (rótulo + quantidade). Ex.: atendimentos por faixa etária.</summary>
    Distribuicao = 4,

    /// <summary>
    /// Linha agrupadora sem cálculo próprio: peso e pontuação são a soma dos filhos
    /// (na planilha, <c>=SUM(G6:G10)</c>). Ex.: "tempo de espera por classificação de risco",
    /// que só existe como soma de 3.1 a 3.5.
    /// </summary>
    Agrupador = 5,

    /// <summary>
    /// Média já calculada pelo SQL, devolvida na coluna <c>valor</c> (ex.: <c>AVG(...)</c>).
    /// Usado para "tempo médio" quando não faz sentido expor a soma bruta como numerador —
    /// a coluna <c>denominador</c> carrega o tamanho da amostra. Na planilha equivale a
    /// numerador ÷ denominador, mas o Oracle já faz a divisão.
    /// </summary>
    Media = 6,
}

/// <summary>
/// Como comparar o resultado apurado com a meta contratual. Vem das fórmulas da planilha
/// (<c>=IF(J3&lt;=5,$G3,0)</c>): a pontuação é tudo-ou-nada.
/// </summary>
public enum MetaOperador
{
    /// <summary>Ex.: "≤ 8h", "≤ 10%".</summary>
    MenorOuIgual = 1,

    /// <summary>Ex.: "≥ 85%".</summary>
    MaiorOuIgual = 2,

    /// <summary>Estritamente menor. A planilha usa isto no vermelho (meta "0 min" → <c>&lt;1</c>).</summary>
    Menor = 3,

    /// <summary>Estritamente maior.</summary>
    Maior = 4,

    /// <summary>Igualdade exata. Ex.: "0 óbitos maternos".</summary>
    Igual = 5,

    /// <summary>Faixa fechada, ex.: "80 a 85%" (ocupação da maternidade).</summary>
    Entre = 6,
}

/// <summary>
/// Estado de implementação do indicador — o que a tela mostra quando não há número.
/// </summary>
public enum SituacaoIndicador
{
    /// <summary>Tem SQL e foi conferido contra dado real.</summary>
    Validado = 1,

    /// <summary>Tem SQL, mas ainda não foi conferido contra dado real.</summary>
    NaoValidado = 2,

    /// <summary>Cadastrado sem SQL — o caminho existe, falta escrever/descobrir.</summary>
    SemMotor = 3,

    /// <summary>O dado não existe no banco de origem (depende de processo: NSP, CCIH, RH, custos).</summary>
    ForaDoBanco = 4,
}
