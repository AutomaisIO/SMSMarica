namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Natureza da vaga do agendamento no SISREG. Deriva da coluna 8 ("Vaga (flag)") do export de
/// agendamentos (<c>expo_solicitacoes</c>): <c>0</c> = Primeira Vez, <c>1</c> = Retorno. Na
/// varredura (<c>cons_agendas</c>) vem em texto ("1ª VEZ" / "RETORNO"), normalizado para cá.
/// <para><c>null</c> = não informado (pedido manual, ou origem que não traz o dado).</para>
/// </summary>
public enum TipoVaga
{
    PrimeiraVez = 0,
    Retorno = 1,
}
