using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMSMais.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipamentoEAgendaRecurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "duracao_consulta_minutos",
                schema: "smsmarica",
                table: "agenda",
                newName: "duracao_slot_minutos");

            migrationBuilder.AddColumn<Guid>(
                name: "tipo_exame_id",
                schema: "smsmarica",
                table: "agendamento",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "medico_nome",
                schema: "smsmarica",
                table: "agenda",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "medico_id",
                schema: "smsmarica",
                table: "agenda",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "especialidade_id",
                schema: "smsmarica",
                table: "agenda",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "finalidade",
                schema: "smsmarica",
                table: "agenda",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "equipamento_id",
                schema: "smsmarica",
                table: "agenda",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipamento",
                schema: "smsmarica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modalidade_dicom = table.Column<int>(type: "integer", nullable: false),
                    identificador_dicom = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipamento", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipamento_unidade_unidade_id",
                        column: x => x.unidade_id,
                        principalSchema: "smsmarica",
                        principalTable: "unidade",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_tipo_exame_id",
                schema: "smsmarica",
                table: "agendamento",
                column: "tipo_exame_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_equipamento_id",
                schema: "smsmarica",
                table: "agenda",
                column: "equipamento_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agenda_recurso_por_finalidade",
                schema: "smsmarica",
                table: "agenda",
                sql: "(finalidade = 1 AND especialidade_id IS NOT NULL AND equipamento_id IS NULL) OR (finalidade = 2 AND equipamento_id IS NOT NULL AND especialidade_id IS NULL AND medico_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_equipamento_excluido_em",
                schema: "smsmarica",
                table: "equipamento",
                column: "excluido_em",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_equipamento_unidade_nome",
                schema: "smsmarica",
                table: "equipamento",
                columns: new[] { "unidade_id", "nome" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_agenda_equipamento_equipamento_id",
                schema: "smsmarica",
                table: "agenda",
                column: "equipamento_id",
                principalSchema: "smsmarica",
                principalTable: "equipamento",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_agendamento_tipo_exame_tipo_exame_id",
                schema: "smsmarica",
                table: "agendamento",
                column: "tipo_exame_id",
                principalSchema: "smsmarica",
                principalTable: "tipo_exame",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agenda_equipamento_equipamento_id",
                schema: "smsmarica",
                table: "agenda");

            migrationBuilder.DropForeignKey(
                name: "FK_agendamento_tipo_exame_tipo_exame_id",
                schema: "smsmarica",
                table: "agendamento");

            migrationBuilder.DropTable(
                name: "equipamento",
                schema: "smsmarica");

            migrationBuilder.DropIndex(
                name: "IX_agendamento_tipo_exame_id",
                schema: "smsmarica",
                table: "agendamento");

            migrationBuilder.DropIndex(
                name: "ix_agenda_equipamento_id",
                schema: "smsmarica",
                table: "agenda");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agenda_recurso_por_finalidade",
                schema: "smsmarica",
                table: "agenda");

            migrationBuilder.DropColumn(
                name: "tipo_exame_id",
                schema: "smsmarica",
                table: "agendamento");

            migrationBuilder.DropColumn(
                name: "finalidade",
                schema: "smsmarica",
                table: "agenda");

            migrationBuilder.DropColumn(
                name: "equipamento_id",
                schema: "smsmarica",
                table: "agenda");

            migrationBuilder.RenameColumn(
                name: "duracao_slot_minutos",
                schema: "smsmarica",
                table: "agenda",
                newName: "duracao_consulta_minutos");

            migrationBuilder.AlterColumn<string>(
                name: "medico_nome",
                schema: "smsmarica",
                table: "agenda",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "medico_id",
                schema: "smsmarica",
                table: "agenda",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "especialidade_id",
                schema: "smsmarica",
                table: "agenda",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
