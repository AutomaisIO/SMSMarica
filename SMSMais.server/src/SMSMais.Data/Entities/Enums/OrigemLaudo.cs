namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Onde nasce o laudo dos exames de um <see cref="Equipamento"/> (ADR-0075). Escolha única de
/// propósito: duas caixas ("habilitar laudo" + "laudo vem com a imagem") deixariam marcar as duas,
/// e não existe exame laudado aqui que também chegue laudado de fora. Persistido como int —
/// valores estáveis.
/// </summary>
public enum OrigemLaudo
{
    /// <summary>O médico lauda e assina pela plataforma (mamografia, densitometria, raio-X). O exame
    /// entra na fila de laudo e conta como "aguardando laudo".</summary>
    Plataforma = 1,

    /// <summary>O estudo já chega com o laudo pronto, assinado fora, no mesmo PDF da imagem (MAPA e
    /// Holter da CardioNet). Não há laudo nem assinatura nossa.</summary>
    JuntoComExame = 2,

    /// <summary>O laudo é feito em outra plataforma e não passa por aqui (os ultrassons: até
    /// 20/10/2026, nenhum dos 1.359 exames de US teve laudo na plataforma).</summary>
    ForaDaPlataforma = 3,
}
