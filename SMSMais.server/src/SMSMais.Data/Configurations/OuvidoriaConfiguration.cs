using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Ouvidoria;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations;

// Módulo Ouvidoria (ADR-0060) — uma configuration por entidade, molde TicketConfiguration.
// A sequência `ouvidoria_protocolo_seq` NÃO fica aqui: é declarada em SmsMaisDbContext.OnModelCreating.

internal sealed class OuvidoriaManifestacaoConfiguration : IEntityTypeConfiguration<OuvidoriaManifestacao>
{
    public void Configure(EntityTypeBuilder<OuvidoriaManifestacao> builder)
    {
        builder.ToTable("ouvidoria_manifestacao");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Protocolo).HasColumnName("protocolo").HasMaxLength(11).IsRequired();
        builder.Property(m => m.CodigoAcessoHash).HasColumnName("codigo_acesso_hash").HasMaxLength(64);

        // Classificação
        builder.Property(m => m.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(m => m.Identificacao).HasColumnName("identificacao").HasConversion<int>().IsRequired();
        builder.Property(m => m.Canal).HasColumnName("canal").HasConversion<int>().IsRequired();
        builder.Property(m => m.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(m => m.Prioridade).HasColumnName("prioridade").HasConversion<int>().IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(m => m.AssuntoId).HasColumnName("assunto_id");
        builder.Property(m => m.SubassuntoId).HasColumnName("subassunto_id");
        builder.Property(m => m.Resumo).HasColumnName("resumo").HasMaxLength(200);
        builder.Property(m => m.Teor).HasColumnName("teor").IsRequired();
        builder.Property(m => m.TeorPseudonimizado).HasColumnName("teor_pseudonimizado");

        // Contexto
        builder.Property(m => m.UnidadeId).HasColumnName("unidade_id");
        builder.Property(m => m.PontoRespostaId).HasColumnName("ponto_resposta_id");
        builder.Property(m => m.RegulacaoSolicitacaoId).HasColumnName("regulacao_solicitacao_id");
        builder.Property(m => m.ProtocoloExterno).HasColumnName("protocolo_externo").HasMaxLength(60);
        builder.Property(m => m.SistemaExterno).HasColumnName("sistema_externo").HasMaxLength(40);
        builder.Property(m => m.DataFato).HasColumnName("data_fato");
        builder.Property(m => m.LocalFato).HasColumnName("local_fato").HasMaxLength(200);

        // Manifestante
        builder.Property(m => m.ManifestanteNome).HasColumnName("manifestante_nome").HasMaxLength(200);
        builder.Property(m => m.ManifestanteCpf).HasColumnName("manifestante_cpf").HasMaxLength(11);
        builder.Property(m => m.ManifestanteTelefone).HasColumnName("manifestante_telefone").HasMaxLength(20);
        builder.Property(m => m.ManifestanteEmail).HasColumnName("manifestante_email").HasMaxLength(200);
        builder.Property(m => m.ManifestantePatientId).HasColumnName("manifestante_patient_id");

        // Referido
        builder.Property(m => m.ReferidoPatientId).HasColumnName("referido_patient_id");
        builder.Property(m => m.ReferidoNome).HasColumnName("referido_nome").HasMaxLength(200);
        builder.Property(m => m.ReferidoCpf).HasColumnName("referido_cpf").HasMaxLength(11);
        builder.Property(m => m.ReferidoCns).HasColumnName("referido_cns").HasMaxLength(15);

        // Envolvido
        builder.Property(m => m.EnvolvidoPractitionerId).HasColumnName("envolvido_practitioner_id");
        builder.Property(m => m.EnvolvidoDescricao).HasColumnName("envolvido_descricao").HasMaxLength(300);

        // Relógios
        builder.Property(m => m.RegistradaEm).HasColumnName("registrada_em").IsRequired();
        builder.Property(m => m.PrazoRespostaEm).HasColumnName("prazo_resposta_em").IsRequired();
        builder.Property(m => m.ProrrogadoEm).HasColumnName("prorrogado_em");
        builder.Property(m => m.ProrrogacaoJustificativa).HasColumnName("prorrogacao_justificativa");
        builder.Property(m => m.PrazoAreaEm).HasColumnName("prazo_area_em");
        builder.Property(m => m.EncaminhadaEm).HasColumnName("encaminhada_em");
        builder.Property(m => m.ComplementacaoSolicitadaEm).HasColumnName("complementacao_solicitada_em");
        builder.Property(m => m.ComplementacaoUsada).HasColumnName("complementacao_usada").HasDefaultValue(false).IsRequired();
        builder.Property(m => m.SuspensaEm).HasColumnName("suspensa_em");
        builder.Property(m => m.DiasSuspensos).HasColumnName("dias_suspensos").HasDefaultValue(0).IsRequired();
        builder.Property(m => m.RespondidaEm).HasColumnName("respondida_em");
        builder.Property(m => m.ConcluidaEm).HasColumnName("concluida_em");
        builder.Property(m => m.DiasAteResposta).HasColumnName("dias_ate_resposta");
        builder.Property(m => m.DiasAtraso).HasColumnName("dias_atraso");
        builder.Property(m => m.UltimaAtividadeEm).HasColumnName("ultima_atividade_em").IsRequired();

        // Conclusão
        builder.Property(m => m.Resolutividade).HasColumnName("resolutividade").HasConversion<int?>();
        builder.Property(m => m.SituacaoFinal).HasColumnName("situacao_final").HasConversion<int?>();
        builder.Property(m => m.MotivoNaoAtendimento).HasColumnName("motivo_nao_atendimento").HasConversion<int?>();
        builder.Property(m => m.MotivoArquivamento).HasColumnName("motivo_arquivamento").HasConversion<int?>();
        builder.Property(m => m.RespostaConclusiva).HasColumnName("resposta_conclusiva");

        // Denúncia
        builder.Property(m => m.HabilitadaEm).HasColumnName("habilitada_em");
        builder.Property(m => m.HabilitadaPor).HasColumnName("habilitada_por");

        // Trabalho
        builder.Property(m => m.ResponsavelId).HasColumnName("responsavel_id");

        // Auditoria ADR-0006
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(m => m.CriadoPor).HasColumnName("criado_por");
        builder.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(m => m.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(m => m.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(m => m.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(m => m.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Índices
        builder.HasIndex(m => m.Protocolo).IsUnique();
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.UnidadeId);
        builder.HasIndex(m => m.PontoRespostaId);
        builder.HasIndex(m => m.PrazoRespostaEm);
        builder.HasIndex(m => m.ManifestanteCpf);
        builder.HasIndex(m => new { m.Tipo, m.RegistradaEm });

        // FKs de referência (Restrict): nada aqui apaga uma manifestação por tabela.
        builder.HasOne(m => m.Assunto)
            .WithMany()
            .HasForeignKey(m => m.AssuntoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Subassunto)
            .WithMany()
            .HasForeignKey(m => m.SubassuntoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Unidade)
            .WithMany()
            .HasForeignKey(m => m.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PontoResposta)
            .WithMany()
            .HasForeignKey(m => m.PontoRespostaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RegulacaoSolicitacao>()
            .WithMany()
            .HasForeignKey(m => m.RegulacaoSolicitacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filhos (Cascade a partir da manifestação)
        builder.HasMany(m => m.Eventos)
            .WithOne(e => e.Manifestacao)
            .HasForeignKey(e => e.ManifestacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Anexos)
            .WithOne(a => a.Manifestacao)
            .HasForeignKey(a => a.ManifestacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Marcadores)
            .WithOne(mm => mm.Manifestacao)
            .HasForeignKey(mm => mm.ManifestacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OuvidoriaEventoConfiguration : IEntityTypeConfiguration<OuvidoriaEvento>
{
    public void Configure(EntityTypeBuilder<OuvidoriaEvento> builder)
    {
        builder.ToTable("ouvidoria_evento");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ManifestacaoId).HasColumnName("manifestacao_id").IsRequired();
        builder.Property(e => e.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(e => e.StatusAnterior).HasColumnName("status_anterior").HasConversion<int?>();
        builder.Property(e => e.StatusNovo).HasColumnName("status_novo").HasConversion<int?>();
        builder.Property(e => e.AutorId).HasColumnName("autor_id");
        builder.Property(e => e.AutorNome).HasColumnName("autor_nome").HasMaxLength(200);
        builder.Property(e => e.PontoRespostaId).HasColumnName("ponto_resposta_id");
        builder.Property(e => e.Texto).HasColumnName("texto");
        builder.Property(e => e.VisivelAoCidadao).HasColumnName("visivel_ao_cidadao").HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(e => new { e.ManifestacaoId, e.CriadoEm });

        builder.HasOne<OuvidoriaPontoResposta>()
            .WithMany()
            .HasForeignKey(e => e.PontoRespostaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OuvidoriaAnexoConfiguration : IEntityTypeConfiguration<OuvidoriaAnexo>
{
    public void Configure(EntityTypeBuilder<OuvidoriaAnexo> builder)
    {
        builder.ToTable("ouvidoria_anexo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ManifestacaoId).HasColumnName("manifestacao_id").IsRequired();
        builder.Property(a => a.EventoId).HasColumnName("evento_id");
        builder.Property(a => a.MidiaId).HasColumnName("midia_id").IsRequired();
        builder.Property(a => a.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(300).IsRequired();
        builder.Property(a => a.VisivelAoCidadao).HasColumnName("visivel_ao_cidadao").HasDefaultValue(false).IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(a => a.ManifestacaoId);

        // Anexo preso a um evento cai junto com ele (o evento já cai junto com a manifestação).
        builder.HasOne<OuvidoriaEvento>()
            .WithMany()
            .HasForeignKey(a => a.EventoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Midia>()
            .WithMany()
            .HasForeignKey(a => a.MidiaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OuvidoriaAssuntoConfiguration : IEntityTypeConfiguration<OuvidoriaAssunto>
{
    public void Configure(EntityTypeBuilder<OuvidoriaAssunto> builder)
    {
        builder.ToTable("ouvidoria_assunto");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.PaiId).HasColumnName("pai_id");
        builder.Property(a => a.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(a => a.CodigoOuvidorSus).HasColumnName("codigo_ouvidor_sus").HasMaxLength(20);
        builder.Property(a => a.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();
        builder.Property(a => a.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();

        builder.HasIndex(a => new { a.PaiId, a.Nome }).IsUnique();

        builder.HasOne(a => a.Pai)
            .WithMany()
            .HasForeignKey(a => a.PaiId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OuvidoriaMarcadorConfiguration : IEntityTypeConfiguration<OuvidoriaMarcador>
{
    public void Configure(EntityTypeBuilder<OuvidoriaMarcador> builder)
    {
        builder.ToTable("ouvidoria_marcador");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Nome).HasColumnName("nome").HasMaxLength(80).IsRequired();
        builder.Property(m => m.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();

        builder.HasIndex(m => m.Nome).IsUnique();
    }
}

internal sealed class OuvidoriaManifestacaoMarcadorConfiguration : IEntityTypeConfiguration<OuvidoriaManifestacaoMarcador>
{
    public void Configure(EntityTypeBuilder<OuvidoriaManifestacaoMarcador> builder)
    {
        builder.ToTable("ouvidoria_manifestacao_marcador");
        builder.HasKey(mm => new { mm.ManifestacaoId, mm.MarcadorId });

        builder.Property(mm => mm.ManifestacaoId).HasColumnName("manifestacao_id");
        builder.Property(mm => mm.MarcadorId).HasColumnName("marcador_id");

        // Lado da manifestação já configurado em OuvidoriaManifestacaoConfiguration (Cascade).
        builder.HasOne(mm => mm.Marcador)
            .WithMany()
            .HasForeignKey(mm => mm.MarcadorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OuvidoriaPontoRespostaConfiguration : IEntityTypeConfiguration<OuvidoriaPontoResposta>
{
    public void Configure(EntityTypeBuilder<OuvidoriaPontoResposta> builder)
    {
        builder.ToTable("ouvidoria_ponto_resposta");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(p => p.UnidadeId).HasColumnName("unidade_id");
        builder.Property(p => p.PrazoDias).HasColumnName("prazo_dias");
        builder.Property(p => p.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();

        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.CriadoPor).HasColumnName("criado_por");
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(p => p.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(p => p.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(p => p.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(p => p.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Uma unidade tem no máximo um ponto de resposta; pontos centrais/apuração não têm unidade.
        builder.HasIndex(p => p.UnidadeId)
            .IsUnique()
            .HasFilter("unidade_id IS NOT NULL");

        builder.HasOne(p => p.Unidade)
            .WithMany()
            .HasForeignKey(p => p.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Membros)
            .WithOne(mb => mb.PontoResposta)
            .HasForeignKey(mb => mb.PontoRespostaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OuvidoriaPontoRespostaMembroConfiguration : IEntityTypeConfiguration<OuvidoriaPontoRespostaMembro>
{
    public void Configure(EntityTypeBuilder<OuvidoriaPontoRespostaMembro> builder)
    {
        builder.ToTable("ouvidoria_ponto_resposta_membro");
        builder.HasKey(mb => mb.Id);

        builder.Property(mb => mb.Id).HasColumnName("id");
        builder.Property(mb => mb.PontoRespostaId).HasColumnName("ponto_resposta_id").IsRequired();
        builder.Property(mb => mb.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(mb => mb.Titular).HasColumnName("titular").HasDefaultValue(false).IsRequired();
        builder.Property(mb => mb.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(mb => new { mb.PontoRespostaId, mb.UsuarioId }).IsUnique();
        builder.HasIndex(mb => mb.UsuarioId);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(mb => mb.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OuvidoriaAcessoIdentidadeConfiguration : IEntityTypeConfiguration<OuvidoriaAcessoIdentidade>
{
    public void Configure(EntityTypeBuilder<OuvidoriaAcessoIdentidade> builder)
    {
        builder.ToTable("ouvidoria_acesso_identidade");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ManifestacaoId).HasColumnName("manifestacao_id").IsRequired();
        builder.Property(a => a.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(a => a.Justificativa).HasColumnName("justificativa").HasMaxLength(500).IsRequired();
        builder.Property(a => a.Ip).HasColumnName("ip").HasMaxLength(45);
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(a => new { a.ManifestacaoId, a.CriadoEm });

        builder.HasOne(a => a.Manifestacao)
            .WithMany()
            .HasForeignKey(a => a.ManifestacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OuvidoriaConfiguracaoConfiguration : IEntityTypeConfiguration<OuvidoriaConfiguracao>
{
    public void Configure(EntityTypeBuilder<OuvidoriaConfiguracao> builder)
    {
        builder.ToTable("ouvidoria_configuracao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PrazoCidadaoDias).HasColumnName("prazo_cidadao_dias").IsRequired();
        builder.Property(c => c.ProrrogacaoDias).HasColumnName("prorrogacao_dias").IsRequired();
        builder.Property(c => c.PrazoAreaDias).HasColumnName("prazo_area_dias").IsRequired();
        builder.Property(c => c.PrazoAreaAltaDias).HasColumnName("prazo_area_alta_dias").IsRequired();
        builder.Property(c => c.PrazoAreaUrgenteDiasUteis).HasColumnName("prazo_area_urgente_dias_uteis").IsRequired();
        builder.Property(c => c.ComplementacaoDias).HasColumnName("complementacao_dias").IsRequired();
        builder.Property(c => c.ArquivamentoAutomaticoDias).HasColumnName("arquivamento_automatico_dias").IsRequired();
        builder.Property(c => c.NotificarPorWhatsApp).HasColumnName("notificar_por_whatsapp").IsRequired();
        builder.Property(c => c.TextoRecibo).HasColumnName("texto_recibo");
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
