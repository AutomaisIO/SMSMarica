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
  MODALIDADES,
  type EquipamentoListItem,
  type ModalidadeDicom,
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
};

function formVazio(unidadeIdFixa?: string): FormEquip {
  return {
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
        enviarCodigo: equipamento.codigoNoProcedimentoSolicitado || equipamento.codigoNoProtocoloAgendado,
        codigoNoProcedimentoSolicitado: equipamento.codigoNoProcedimentoSolicitado,
        codigoNoProtocoloAgendado: equipamento.codigoNoProtocoloAgendado,
        codigoProcedimentoEsquema: equipamento.codigoProcedimentoEsquema ?? '',
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
    if (form.enviarCodigo && !form.codigoNoProcedimentoSolicitado && !form.codigoNoProtocoloAgendado) {
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
      codigoNoProcedimentoSolicitado: form.enviarCodigo && form.codigoNoProcedimentoSolicitado,
      codigoNoProtocoloAgendado: form.enviarCodigo && form.codigoNoProtocoloAgendado,
      codigoProcedimentoEsquema: form.enviarCodigo ? form.codigoProcedimentoEsquema.trim() || null : null,
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
              onChange={(e) => setForm((f) => ({ ...f, modalidadeDicom: e.target.value as ModalidadeDicom }))}
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

        <fieldset className="space-y-3 rounded-md border border-gray-200 p-3">
          <legend className="px-1 text-sm font-medium text-gray-800">Código do procedimento na worklist</legend>
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
              Enviar o código do procedimento em <strong>(0008,0100)</strong>
              <span className="block text-xs text-gray-500">
                Cada exame vai com o código interno do SISREG do seu procedimento, para o aparelho escolher o
                protocolo sozinho. Ligue só se este aparelho foi cadastrado com esses códigos (a lista do
                fornecedor). Desligado, o item da worklist sai exatamente como sempre saiu.
              </span>
            </span>
          </label>

          {form.enviarCodigo ? (
            <div className="space-y-3 pl-6">
              <div className="space-y-1.5 text-sm text-gray-700">
                <span className="text-xs font-medium text-gray-600">Em que sequência o aparelho lê o código</span>
                <label className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={form.codigoNoProcedimentoSolicitado}
                    onChange={(e) => setForm((f) => ({ ...f, codigoNoProcedimentoSolicitado: e.target.checked }))}
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
            </div>
          ) : null}
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
