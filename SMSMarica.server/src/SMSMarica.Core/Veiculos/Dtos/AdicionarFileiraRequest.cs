namespace SMSMarica.Core.Veiculos.Dtos;

/// <summary>
/// Adiciona uma fileira ao veículo. <c>Ordem</c> é o índice visual da fileira
/// (1..N, crescente da frente para trás). Assentos são gerados automaticamente
/// numerados 1..<c>QuantidadeAssentos</c> com tipo Passageiro.
/// </summary>
public sealed record AdicionarFileiraRequest(
    int Ordem,
    int QuantidadeAssentos);
