using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Tests.Infraestrutura;

/// <summary>
/// Seed mínimo de uma SolicitacaoExame válida (Procedimento SIGTAP → TipoExame → Unidade →
/// Solicitação) para testes de integração. Identificadores aleatórios por chamada — os testes
/// compartilham o mesmo Postgres da fixture, então nada aqui pode colidir em índice único.
/// </summary>
internal static class SeedSolicitacao
{
    public static async Task<SolicitacaoExame> CriarAsync(
        SmsMaricaDbContext db,
        Guid pacienteId,
        bool enviarParaWorklist = true,
        DateTime? dataAgendada = null,
        StatusConfirmacaoAgendamento confirmacao = StatusConfirmacaoAgendamento.Pendente)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        var proc = new ProcedimentoSigtap
        {
            Id = Guid.NewGuid(),
            Codigo = $"02.04.{sufixo[..2]}.{sufixo[2..5]}-{sufixo[5]}",
            Nome = $"PROC TESTE {sufixo}",
        };
        var tipo = new TipoExame
        {
            Id = Guid.NewGuid(),
            Nome = $"Exame Teste {sufixo}",
            ProcedimentoSigtapId = proc.Id,
            ModalidadeDicom = ModalidadeDicom.MG,
            EnviarParaWorklist = enviarParaWorklist,
            CriadoEm = DateTime.UtcNow,
        };
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE TESTE {sufixo}",
            CriadoEm = DateTime.UtcNow,
        };
        var solicitacao = new SolicitacaoExame
        {
            Id = Guid.NewGuid(),
            AccessionNumber = $"T{sufixo}",
            StudyInstanceUID = $"2.25.{Random.Shared.NextInt64(1_000_000_000):D10}{Random.Shared.NextInt64(1_000_000_000):D10}",
            PacienteId = pacienteId,
            TipoExameId = tipo.Id,
            UnidadeId = unidade.Id,
            SolicitanteNome = "DR TESTE",
            Status = StatusSolicitacaoExame.Solicitada,
            StatusConfirmacao = confirmacao,
            DataAgendada = dataAgendada,
            ProximaTentativaEm = null, // fluxo novo: nada vai ao PACS sem autorização
            CriadoEm = DateTime.UtcNow,
        };

        db.ProcedimentosSigtap.Add(proc);
        db.TiposExame.Add(tipo);
        db.Unidades.Add(unidade);
        db.SolicitacoesExame.Add(solicitacao);
        await db.SaveChangesAsync();
        return solicitacao;
    }

    /// <summary>CPF sintético de 11 dígitos, único por chamada (sem validação de DV nos serviços).</summary>
    public static string CpfAleatorio() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    /// <summary>Telefone celular BR canônico único (55 21 9XXXX XXXX).</summary>
    public static string TelefoneAleatorio() => $"55219{Random.Shared.Next(10_000_000, 99_999_999)}";
}
