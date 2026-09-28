using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Memória das tentativas de completar uma ficha SEM CPF via CADSUS (importação que acha o
/// paciente por CNS numa ficha incompleta — caso Marcia, 28/09/2026: o desafio de verificação
/// perguntava um CPF que a ficha comparada não tinha, e a pessoa queimava as chances
/// respondendo certo).
///
/// Existe por dois motivos: a consulta tem custo (orçamento anti-robô quando a fonte é o
/// SISREG) e o desfecho "repontado" precisa sobreviver entre varreduras — a ficha-sombra
/// continua sendo encontrada por CNS, e é esta linha que redireciona para o cadastro certo
/// sem nova consulta.
/// </summary>
public class CadsusCompletude
{
    /// <summary>CNS consultado (15 dígitos) — chave natural; uma memória por CNS.</summary>
    public string Cns { get; set; } = string.Empty;

    public DateTime ConsultadoEm { get; set; }

    public DesfechoCadsusCompletude Desfecho { get; set; }

    /// <summary>Só quando <see cref="DesfechoCadsusCompletude.RepontadoParaExistente"/>:
    /// o cadastro (com CPF) para o qual este CNS resolve.</summary>
    public Guid? PacienteDestinoId { get; set; }
}
