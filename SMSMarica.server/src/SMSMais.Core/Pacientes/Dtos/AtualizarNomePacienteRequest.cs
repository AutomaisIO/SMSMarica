namespace SMSMais.Core.Pacientes.Dtos;

/// <summary>
/// Correção do nome oficial do paciente. O nome é normalmente imutável
/// (definido no gate de cadastro via consulta Receita) e por isso NÃO faz parte
/// de <see cref="AtualizarPacienteRequest"/>. Este request existe só para o
/// fluxo "Verificar nome", que recheca o CPF no motor de busca e permite
/// corrigir — e a correção fica registrada na trilha de auditoria.
/// </summary>
public sealed record AtualizarNomePacienteRequest(string NomeCompleto);
