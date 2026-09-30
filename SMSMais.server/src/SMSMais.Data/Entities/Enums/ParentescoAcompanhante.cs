namespace SMSMais.Data.Entities.Enums;

/// <summary>Relação do acompanhante com o paciente. Valor inteiro estável (gravado no banco).</summary>
public enum ParentescoAcompanhante
{
    Mae = 1,
    Pai = 2,
    Filho = 3,
    Conjuge = 4,
    Irmao = 5,
    OutroParente = 6,
    Cuidador = 7,
    Outro = 99,
}
