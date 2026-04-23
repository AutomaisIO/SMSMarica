namespace SMSMarica.Core.Tratamentos.Dtos;

/// <summary>
/// Só permite alterar a descrição. Periodicidade é imutável — para mudar
/// cadência, encerre o tratamento e crie um novo (regra do domínio).
/// </summary>
public sealed record AtualizarTratamentoRequest(
    string Descricao);
