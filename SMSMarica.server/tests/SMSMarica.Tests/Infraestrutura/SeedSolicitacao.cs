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
    public static async Task<ExameImagem> CriarAsync(
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
        // Espinha de regulação + satélite de execução de imagem (ADR-0021).
        var solicitacao = new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            Categoria = CategoriaSolicitacao.Imagem,
            UnidadeExecutanteId = unidade.Id,
            SolicitanteNome = "DR TESTE",
            Status = StatusSolicitacao.Solicitada,
            StatusConfirmacao = confirmacao,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = dataAgendada,
            CriadoEm = DateTime.UtcNow,
        };
        var exame = new ExameImagem
        {
            Id = Guid.NewGuid(),
            Solicitacao = solicitacao,
            AccessionNumber = $"T{sufixo}",
            StudyInstanceUID = $"2.25.{Random.Shared.NextInt64(1_000_000_000):D10}{Random.Shared.NextInt64(1_000_000_000):D10}",
            TipoExameId = tipo.Id,
            Status = StatusSolicitacaoExame.Solicitada,
            ProximaTentativaEm = null, // fluxo novo: nada vai ao PACS sem autorização
            CriadoEm = DateTime.UtcNow,
        };

        db.ProcedimentosSigtap.Add(proc);
        db.TiposExame.Add(tipo);
        db.Unidades.Add(unidade);
        db.Solicitacoes.Add(solicitacao);
        db.ExamesImagem.Add(exame);
        await db.SaveChangesAsync();
        return exame;
    }

    /// <summary>CPF sintético de 11 dígitos, único por chamada (sem validação de DV nos serviços).</summary>
    public static string CpfAleatorio() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    /// <summary>Telefone celular BR canônico único (55 21 9XXXX XXXX).</summary>
    public static string TelefoneAleatorio() => $"55219{Random.Shared.Next(10_000_000, 99_999_999)}";
}
