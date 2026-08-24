using SMSMais.Data.Entities;

namespace SMSMarica.Core.Common.Dtos;

public sealed record EnderecoDto(
    string Cep,
    string Logradouro,
    string? Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Uf,
    string? PontoReferencia)
{
    public static EnderecoDto ParaDto(Endereco e) => new(
        e.Cep, e.Logradouro, e.Numero, e.Complemento,
        e.Bairro, e.Cidade, e.Uf, e.PontoReferencia);

    public Endereco ParaEntidade() => new()
    {
        Cep = (Cep ?? string.Empty).Trim(),
        Logradouro = (Logradouro ?? string.Empty).Trim(),
        Numero = string.IsNullOrWhiteSpace(Numero) ? null : Numero.Trim(),
        Complemento = string.IsNullOrWhiteSpace(Complemento) ? null : Complemento.Trim(),
        Bairro = (Bairro ?? string.Empty).Trim(),
        Cidade = (Cidade ?? string.Empty).Trim(),
        Uf = (Uf ?? string.Empty).Trim().ToUpperInvariant(),
        PontoReferencia = string.IsNullOrWhiteSpace(PontoReferencia) ? null : PontoReferencia.Trim(),
    };
}
