namespace SMSMarica.Core.Integracoes.Sisreg.Dtos;

/// <summary>Resultado de um teste de conexão com o SISREG (não lança — sinaliza sucesso/erro).</summary>
public sealed record TestarConexaoSisregResultado(bool Sucesso, string Mensagem);
