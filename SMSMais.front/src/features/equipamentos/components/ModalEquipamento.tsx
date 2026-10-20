import { useEffect, useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { Select } from '@/shared/ui/Select';
import { useListarUnidades } from '@/features/unidades/api/queries';
import {
  useAtualizarEquipamento,
  useCadastrarEquipamento,
} from '@/features/equipamentos/api/queries';
import {
  CODIGO_ESQUEMA_PADRAO,
  DESCRICAO_MAX_MINIMO,
  DESCRICAO_MAX_PADRAO,
  LIBERACOES_PACIENTE,
  MODALIDADES,
  ORIGENS_LAUDO,
  ORIGENS_UNIDADE,
  TAG_CODIGO_PADRAO,
  TAG_MEDICO_PADRAO,
  TAG_UNIDADE_PADRAO,
  normalizarTag,
  regraPadraoDaModalidade,
  tagCodigoEhPadrao,
  type EquipamentoListItem,
  type LiberacaoExamePaciente,
  type ModalidadeDicom,
  type OrigemLaudo,
  type OrigemUnidadeWorklist,
} from '@/features/equipamentos/types';

type FormEquip = {
  id: string | null;
  nome: string;
  unidadeId: string;
  modalidadeDicom: ModalidadeDicom;
  identificadorDicom: string;
  descricaoMaxCaracteres: number;
  ativo: boolean;
  /** Liga o código do procedimento (0008,0100) na worklist deste aparelho — ADR-0074. */
  enviarCodigo: boolean;
  codigoNoProcedimentoSolicitado: boolean;
  codigoNoProtocoloAgendado: boolean;
  codigoProcedimentoEsquema: string;
  /** Onde vai o código: 0008,0100 = na sequência (o padrão); outra tag = como texto nela. */
  codigoTag: string;
  /** Médico solicitante e unidade ("clínica") — adendo do ADR-0074, cada um na tag que o aparelho lê. */
  enviarMedico: boolean;
  medicoTag: string;
  enviarUnidade: boolean;
  unidadeOrigem: OrigemUnidadeWorklist;
  unidadeTag: string;
  /** Depois que o exame chega (ADR-0075): onde nasce o laudo, quando o paciente recebe, anamnese. */
  laudo: OrigemLaudo;
  liberacaoPaciente: LiberacaoExamePaciente;
  usaAnamnese: boolean;
  /** No aparelho novo, a regra acompanha a modalidade até alguém mexer nela. */
  regraTocada: boolean;
};

function formVazio(unidadeIdFixa?: string): FormEquip {
  return {
    ...regraPadraoDaModalidade('US'),
    regraTocada: false,
    id: null,
    nome: '',
    unidadeId: unidadeIdFixa ?? '',
    modalidadeDicom: 'US',
    identificadorDicom: '',
    descricaoMaxCaracteres: DESCRICAO_MAX_PADRAO,
    ativo: true,
    enviarCodigo: false,
    codigoNoProcedimentoSolicitado: false,
    codigoNoProtocoloAgendado: false,
    codigoProcedimentoEsquema: '',
    codigoTag: TAG_CODIGO_PADRAO,
    enviarMedico: false,
    medicoTag: TAG_MEDICO_PADRAO,
    enviarUnidade: false,
    unidadeOrigem: 'Executante',
    unidadeTag: TAG_UNIDADE_PADRAO.Executante,
  };
}

type Props = {
  aberto: boolean;
  aoFechar: () => void;
  /** Equipamento em edição; `null` = novo. */
  equipamento: EquipamentoListItem | null;
  /**
   * Quando informado, o equipamento é sempre desta unidade e o seletor de
   * unidade não aparece (uso dentro do detalhe da unidade). Ausente = página
   * standalone, com seletor de unidade livre.
   */
  unidadeIdFixa?: string;
};

/**
 * Modal de inclusão/edição de equipamento de imagem. Compartilhado entre a
 * página standalone (`EquipamentosPage`) e a aba Equipamentos do detalhe da
 * unidade (`EquipamentosDaUnidadeSecao`).
 */
export function ModalEquipamento({ aberto, aoFechar, equipamento, unidadeIdFixa }: Props) {
  const unidades = useListarUnidades();
  const cadastrar = useCadastrarEquipamento();
  const atualizar = useAtualizarEquipamento();

  const [form, setForm] = useState<FormEquip>(formVazio(unidadeIdFixa));
  const [erro, setErro] = useState<string | null>(null);

  // Sincroniza o form ao abrir (novo x edição) e limpa erro ao fechar.
  useEffect(() => {
    if (!aberto) {
      setErro(null);
      return;
    }
    if (equipamento) {
      setForm({
        id: equipamento.id,
        nome: equipamento.nome,
        unidadeId: equipamento.unidadeId,
        modalidadeDicom: equipamento.modalidadeDicom,
        identificadorDicom: equipamento.identificadorDicom ?? '',
        descricaoMaxCaracteres: equipamento.descricaoMaxCaracteres ?? DESCRICAO_MAX_PADRAO,
        ativo: equipamento.ativo,
        enviarCodigo:
          equipamento.codigoNoProcedimentoSolicitado ||
          equipamento.codigoNoProtocoloAgendado ||
          equipamento.codigoProcedimentoTag !== null,
        codigoNoProcedimentoSolicitado: equipamento.codigoNoProcedimentoSolicitado,
        codigoNoProtocoloAgendado: equipamento.codigoNoProtocoloAgendado,
        codigoProcedimentoEsquema: equipamento.codigoProcedimentoEsquema ?? '',
        codigoTag: equipamento.codigoProcedimentoTag ?? TAG_CODIGO_PADRAO,
        enviarMedico: equipamento.medicoSolicitanteTag !== null,
        medicoTag: equipamento.medicoSolicitanteTag ?? TAG_MEDICO_PADRAO,
        enviarUnidade: equipamento.unidadeTag !== null,
        unidadeOrigem: equipamento.unidadeOrigem ?? 'Executante',
        unidadeTag: equipamento.unidadeTag ?? TAG_UNIDADE_PADRAO[equipamento.unidadeOrigem ?? 'Executante'],
        laudo: equipamento.laudo ?? 'Plataforma',
        liberacaoPaciente: equipamento.liberacaoPaciente ?? 'ComLaudoAssinado',
        usaAnamnese: equipamento.usaAnamnese ?? true,
        regraTocada: true,
      });
    } else {
      setForm(formVazio(unidadeIdFixa));
    }
    setErro(null);
  }, [aberto, equipamento, unidadeIdFixa]);

  function aoSalvar(ev: FormEvent) {
    ev.preventDefault();
    setErro(null);
    const unidadeId = unidadeIdFixa ?? form.unidadeId;
    if (!unidadeId) {
      setErro('Selecione a unidade.');
      return;
    }
    const codigoNaSequencia = tagCodigoEhPadrao(form.codigoTag);
    if (
      form.enviarCodigo &&
      codigoNaSequencia &&
      !form.codigoNoProcedimentoSolicitado &&
      !form.codigoNoProtocoloAgendado
    ) {
      setErro('Marque em que sequência o código do procedimento vai (ou desligue o envio do código).');
      return;
    }
    const payload = {
      nome: form.nome.trim(),
      unidadeId,
      modalidadeDicom: form.modalidadeDicom,
      identificadorDicom: form.identificadorDicom.trim() || null,
      descricaoMaxCaracteres: form.descricaoMaxCaracteres,
      ativo: form.ativo,
      codigoNoProcedimentoSolicitado: form.enviarCodigo && codigoNaSequencia && form.codigoNoProcedimentoSolicitado,
      codigoNoProtocoloAgendado: form.enviarCodigo && codigoNaSequencia && form.codigoNoProtocoloAgendado,
      codigoProcedimentoEsquema:
        form.enviarCodigo && codigoNaSequencia ? form.codigoProcedimentoEsquema.trim() || null : null,
      // Sempre mandado (o servidor trata ausente como "mantém"): o padrão desliga a tag de texto.
      codigoProcedimentoTag: form.enviarCodigo && !codigoNaSequencia ? form.codigoTag.trim() : TAG_CODIGO_PADRAO,
      enviarMedicoSolicitante: form.enviarMedico,
      medicoSolicitanteTag: form.enviarMedico ? form.medicoTag.trim() || null : null,
      enviarUnidade: form.enviarUnidade,
      unidadeOrigem: form.unidadeOrigem,
      unidadeTag: form.enviarUnidade ? form.unidadeTag.trim() || null : null,
      laudo: form.laudo,
      liberacaoPaciente: form.liberacaoPaciente,
      usaAnamnese: form.usaAnamnese,
    };
    const opcoes = {
      onSuccess: () => aoFechar(),
      onError: (err: unknown) => setErro(extrairMensagemDeErro(err)),
    };
    if (form.id) atualizar.mutate({ id: form.id, payload }, opcoes);
    else cadastrar.mutate(payload, opcoes);
  }

  const salvando = cadastrar.isPending || atualizar.isPending;

  return (
    <Modal aberto={aberto} aoFechar={aoFechar} titulo={form.id ? 'Editar equipamento' : 'Novo equipamento'}>
      <form onSubmit={aoSalvar} className="space-y-4">
        <Campo label="Nome" htmlFor="eq-nome" required>
          <Input
            id="eq-nome"
            value={form.nome}
            onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))}
            placeholder="Ex.: Ultrassom Sala 2"
            autoFocus
            required
          />
        </Campo>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {unidadeIdFixa ? null : (
            <Campo label="Unidade" htmlFor="eq-unidade" required>
              <Select
                id="eq-unidade"
                value={form.unidadeId}
                onChange={(e) => setForm((f) => ({ ...f, unidadeId: e.target.value }))}
              >
                <option value="">Selecione…</option>
                {(unidades.data ?? [])
                  .filter((u) => u.ativo)
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.nome}
                    </option>
                  ))}
              </Select>
            </Campo>
          )}

          <Campo label="Modalidade" htmlFor="eq-modalidade" required>
            <Select
              id="eq-modalidade"
              value={form.modalidadeDicom}
              onChange={(e) => {
                const modalidade = e.target.value as ModalidadeDicom;
                // Aparelho novo: a regra de laudo e liberação acompanha a modalidade (ultrassom é
                // laudado fora e libera ao chegar) até alguém escolher outra.
                setForm((f) => ({
                  ...f,
                  modalidadeDicom: modalidade,
                  ...(f.id || f.regraTocada ? {} : regraPadraoDaModalidade(modalidade)),
                }));
              }}
            >
              {MODALIDADES.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.rotulo}
                </option>
              ))}
            </Select>
          </Campo>
        </div>

        <Campo
          label="AE Title (identificador DICOM)"
          htmlFor="eq-dicom"
          dica="AE Title configurado no próprio equipamento. É por ele que a worklist chega só nesta máquina — em branco, os exames desta unidade caem no AE padrão do sistema. Até 16 caracteres, sem espaços nem acentos."
        >
          <Input
            id="eq-dicom"
            value={form.identificadorDicom}
            onChange={(e) => setForm((f) => ({ ...f, identificadorDicom: e.target.value }))}
            placeholder="Ex.: US_CMI"
          />
        </Campo>

        <Campo
          label="Limite da descrição do exame"
          htmlFor="eq-desc-max"
          dica="Quantos caracteres da descrição do exame este aparelho aguenta receber na worklist. O padrão 64 é o teto do próprio DICOM — deixe assim. Só baixe se o console DESTE aparelho falhar com descrição longa: é o caso do mamógrafo Fuji FDR-3000AWS, que fica em 16. Valor menor corta o nome do exame na tela do técnico."
        >
          <Input
            id="eq-desc-max"
            type="number"
            min={DESCRICAO_MAX_MINIMO}
            max={DESCRICAO_MAX_PADRAO}
            value={form.descricaoMaxCaracteres}
            onChange={(e) =>
              setForm((f) => ({
                ...f,
                descricaoMaxCaracteres: Number(e.target.value) || DESCRICAO_MAX_PADRAO,
              }))
            }
          />
        </Campo>

        <fieldset className="space-y-4 rounded-md border border-gray-200 p-3">
          <legend className="px-1 text-sm font-medium text-gray-800">O que vai na worklist deste aparelho</legend>
          <p className="text-xs text-gray-500">
            Cada campo vai na tag DICOM que o aparelho lê. A tag já vem com o padrão — mude só se o técnico do
            aparelho pedir outra. Tudo desligado, o item da worklist sai exatamente como sempre saiu.
          </p>

          <div className="space-y-3">
            <label className="flex items-start gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={form.enviarCodigo}
                onChange={(e) => {
                  const ligar = e.target.checked;
                  // Ao ligar, as duas sequências já vêm marcadas: cada aparelho lê numa, e mandar
                  // nas duas é inofensivo quando não se sabe qual.
                  setForm((f) => ({
                    ...f,
                    enviarCodigo: ligar,
                    codigoNoProcedimentoSolicitado: ligar ? true : f.codigoNoProcedimentoSolicitado,
                    codigoNoProtocoloAgendado: ligar ? true : f.codigoNoProtocoloAgendado,
                  }));
                }}
              />
              <span>
                Código do procedimento
                <span className="block text-xs text-gray-500">
                  O código interno do SISREG de cada exame, para o aparelho escolher o protocolo sozinho. Ligue só
                  se este aparelho foi cadastrado com esses códigos (a lista do fornecedor).
                </span>
              </span>
            </label>

            {form.enviarCodigo ? (
              <div className="space-y-3 pl-6">
                <Campo
                  label="Tag DICOM do código"
                  htmlFor="eq-codigo-tag"
                  dica={`Padrão ${TAG_CODIGO_PADRAO}: o código vai dentro da sequência de código, com esquema e descrição. Outra tag (ex.: 0040,2400): o código vai como texto simples nela.`}
                >
                  <Input
                    id="eq-codigo-tag"
                    value={form.codigoTag}
                    onChange={(e) => setForm((f) => ({ ...f, codigoTag: e.target.value }))}
                    placeholder={TAG_CODIGO_PADRAO}
                    maxLength={11}
                    className="font-mono"
                  />
                </Campo>
                {tagCodigoEhPadrao(form.codigoTag) ? (
                  <>
                    <div className="space-y-1.5 text-sm text-gray-700">
                      <span className="text-xs font-medium text-gray-600">Em que sequência o aparelho lê o código</span>
                      <label className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={form.codigoNoProcedimentoSolicitado}
                          onChange={(e) =>
                            setForm((f) => ({ ...f, codigoNoProcedimentoSolicitado: e.target.checked }))
                          }
                        />
                        Procedimento solicitado — (0032,1064) › (0008,0100)
                      </label>
                      <label className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={form.codigoNoProtocoloAgendado}
                          onChange={(e) => setForm((f) => ({ ...f, codigoNoProtocoloAgendado: e.target.checked }))}
                        />
                        Protocolo agendado — (0040,0100) › (0040,0008) › (0008,0100)
                      </label>
                    </div>
                    <Campo
                      label="Esquema do código (0008,0102)"
                      htmlFor="eq-codigo-esquema"
                      dica={`Identifica de onde vem o código. Em branco vale ${CODIGO_ESQUEMA_PADRAO} (esquema privado). Só mude se o fornecedor pedir outro. Até 16 caracteres: letras, números, ponto, hífen ou sublinhado.`}
                    >
                      <Input
                        id="eq-codigo-esquema"
                        value={form.codigoProcedimentoEsquema}
                        onChange={(e) => setForm((f) => ({ ...f, codigoProcedimentoEsquema: e.target.value }))}
                        placeholder={CODIGO_ESQUEMA_PADRAO}
                        maxLength={16}
                      />
                    </Campo>
                  </>
                ) : (
                  <p className="text-xs text-gray-500">
                    O código vai como texto em ({form.codigoTag.trim()}), sem sequência e sem esquema.
                  </p>
                )}
              </div>
            ) : null}
          </div>

          <div className="space-y-3">
            <label className="flex items-start gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={form.enviarMedico}
                onChange={(e) => setForm((f) => ({ ...f, enviarMedico: e.target.checked }))}
              />
              <span>
                Médico solicitante
                <span className="block text-xs text-gray-500">
                  O nome de quem pediu o exame, como veio no pedido.
                </span>
              </span>
            </label>
            {form.enviarMedico ? (
              <div className="pl-6">
                <Campo
                  label="Tag DICOM do médico"
                  htmlFor="eq-medico-tag"
                  dica={`Padrão ${TAG_MEDICO_PADRAO} (Referring Physician's Name) — é o que o CardioNet e a maioria dos consoles mostram.`}
                >
                  <Input
                    id="eq-medico-tag"
                    value={form.medicoTag}
                    onChange={(e) => setForm((f) => ({ ...f, medicoTag: e.target.value }))}
                    placeholder={TAG_MEDICO_PADRAO}
                    maxLength={11}
                    className="font-mono"
                  />
                </Campo>
              </div>
            ) : null}
          </div>

          <div className="space-y-3">
            <label className="flex items-start gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={form.enviarUnidade}
                onChange={(e) => setForm((f) => ({ ...f, enviarUnidade: e.target.checked }))}
              />
              <span>
                Unidade (clínica)
                <span className="block text-xs text-gray-500">
                  O nome da unidade — a executante ou a que pediu o exame, conforme a origem escolhida.
                </span>
              </span>
            </label>
            {form.enviarUnidade ? (
              <div className="grid grid-cols-1 gap-3 pl-6 sm:grid-cols-2">
                <Campo label="Origem do dado" htmlFor="eq-unidade-origem">
                  <Select
                    id="eq-unidade-origem"
                    value={form.unidadeOrigem}
                    onChange={(e) => {
                      const origem = e.target.value as OrigemUnidadeWorklist;
                      // Tag ainda no padrão da origem anterior (ou vazia) acompanha a troca;
                      // tag escolhida à mão fica.
                      setForm((f) => {
                        const tagAtual = normalizarTag(f.unidadeTag);
                        const noPadrao = !f.unidadeTag.trim() || tagAtual === normalizarTag(TAG_UNIDADE_PADRAO[f.unidadeOrigem]);
                        return { ...f, unidadeOrigem: origem, unidadeTag: noPadrao ? TAG_UNIDADE_PADRAO[origem] : f.unidadeTag };
                      });
                    }}
                  >
                    {ORIGENS_UNIDADE.map((o) => (
                      <option key={o.id} value={o.id}>
                        {o.rotulo}
                      </option>
                    ))}
                  </Select>
                </Campo>
                <Campo
                  label="Tag DICOM da unidade"
                  htmlFor="eq-unidade-tag"
                  dica={`Padrão ${TAG_UNIDADE_PADRAO.Executante} (Institution Name) para a executante e ${TAG_UNIDADE_PADRAO.Solicitante} (Requesting Service) para a solicitante.`}
                >
                  <Input
                    id="eq-unidade-tag"
                    value={form.unidadeTag}
                    onChange={(e) => setForm((f) => ({ ...f, unidadeTag: e.target.value }))}
                    placeholder={TAG_UNIDADE_PADRAO[form.unidadeOrigem]}
                    maxLength={11}
                    className="font-mono"
                  />
                </Campo>
              </div>
            ) : null}
          </div>
        </fieldset>

        <fieldset className="space-y-4 rounded-md border border-gray-200 p-3">
          <legend className="px-1 text-sm font-medium text-gray-800">Depois que o exame chega</legend>
          <p className="text-xs text-gray-500">
            Vale para todos os exames feitos neste aparelho. Mudar aqui vale daqui para a frente: o exame que já
            foi liberado ao paciente continua liberado, e o retido continua retido até o laudo (ou até alguém
            liberar no detalhe da solicitação).
          </p>

          <div className="space-y-1.5">
            <span className="text-xs font-medium text-gray-600">Laudo</span>
            {ORIGENS_LAUDO.map((o) => (
              <label key={o.id} className="flex items-start gap-2 text-sm text-gray-700">
                <input
                  type="radio"
                  name="eq-laudo"
                  className="mt-0.5"
                  checked={form.laudo === o.id}
                  onChange={() =>
                    // Sem laudo nosso, "só com o laudo" prenderia o exame para sempre: troca junto.
                    setForm((f) => ({
                      ...f,
                      laudo: o.id,
                      liberacaoPaciente: o.id === 'Plataforma' ? f.liberacaoPaciente : 'AoChegar',
                      regraTocada: true,
                    }))
                  }
                />
                <span>
                  {o.rotulo}
                  <span className="block text-xs text-gray-500">{o.dica}</span>
                </span>
              </label>
            ))}
          </div>

          <div className="space-y-1.5">
            <span className="text-xs font-medium text-gray-600">Quando o paciente recebe o exame</span>
            {LIBERACOES_PACIENTE.map((l) => {
              const bloqueada = l.id === 'ComLaudoAssinado' && form.laudo !== 'Plataforma';
              return (
                <label
                  key={l.id}
                  className={`flex items-start gap-2 text-sm ${bloqueada ? 'text-gray-400' : 'text-gray-700'}`}
                  title={
                    bloqueada
                      ? 'Só existe para exame laudado na plataforma: sem laudo nosso, o exame ficaria retido para sempre.'
                      : undefined
                  }
                >
                  <input
                    type="radio"
                    name="eq-liberacao"
                    className="mt-0.5"
                    checked={form.liberacaoPaciente === l.id}
                    disabled={bloqueada}
                    onChange={() => setForm((f) => ({ ...f, liberacaoPaciente: l.id, regraTocada: true }))}
                  />
                  <span>
                    {l.rotulo}
                    <span className="block text-xs text-gray-500">{l.dica}</span>
                  </span>
                </label>
              );
            })}
          </div>

          <label className="flex items-start gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={form.usaAnamnese}
              onChange={(e) => setForm((f) => ({ ...f, usaAnamnese: e.target.checked, regraTocada: true }))}
            />
            <span>
              Usa anamnese
              <span className="block text-xs text-gray-500">
                Desligado, o botão de anamnese some dos exames deste aparelho e o laudo não a cobra (MAPA e Holter
                não têm questionário).
              </span>
            </span>
          </label>
        </fieldset>

        {form.id ? (
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={form.ativo}
              onChange={(e) => setForm((f) => ({ ...f, ativo: e.target.checked }))}
            />
            Ativo
          </label>
        ) : null}

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}

        <div className="flex items-center justify-end gap-3 pt-2">
          <Button type="button" variante="secundaria" onClick={aoFechar}>
            Cancelar
          </Button>
          <Button type="submit" disabled={salvando || !form.nome.trim()}>
            {salvando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Salvar
          </Button>
        </div>
      </form>
    </Modal>
  );
}
