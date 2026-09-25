namespace SMSMais.Core.TiposExame;

/// <summary>
/// Forma canônica do nome de um <see cref="Data.Entities.TipoExame"/>: MAIÚSCULAS, sem espaço
/// sobrando. É a MESMA regra que a importação do SISREG sempre aplicou
/// (<see cref="Integracoes.SisregWeb.Importacao.ResolvedorTipoExameSisreg.NormalizarNome"/>) — agora
/// compartilhada com o cadastro manual (<see cref="TiposExameService"/>) para que os dois lados
/// produzam a MESMA chave.
///
/// <para>O porquê: até 25/09/2026 a importação gravava em maiúsculas e o cadastro manual gravava
/// como o operador digitava, com a checagem de duplicado sensível à caixa. Assim nasceram pares
/// como "Ultrassom de tireoide" × "ULTRASSOM DE TIREOIDE" — dois tipos para o mesmo procedimento,
/// só que a solicitação importada casava com um e o escopo da unidade estava ligado no outro, e o
/// exame não ia à worklist. Comparar e gravar sempre em caixa alta fecha essa porta. (Não funde
/// duplicatas que diferem por PALAVRA, ex.: "Ultrassom transvaginal" × "ULTRASONOGRAFIA
/// TRANSVAGINAL" — essas são reconciliação de catálogo, ADR-0055.)</para>
/// </summary>
public static class NomeTipoExame
{
    public const int TamanhoMaximo = 200;

    /// <summary>
    /// Nome em MAIÚSCULAS com espaços colapsados, truncado em <see cref="TamanhoMaximo"/>. O colapso
    /// de espaços importa: o export do SISREG traz "CONSULTA  EM CARDIOLOGIA" com espaço duplo, e sem
    /// isto a mesma coisa viraria dois tipos.
    /// </summary>
    public static string Normalizar(string? bruto)
    {
        var limpo = string.Join(' ', (bruto ?? string.Empty).ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return limpo.Length > TamanhoMaximo ? limpo[..TamanhoMaximo] : limpo;
    }
}
