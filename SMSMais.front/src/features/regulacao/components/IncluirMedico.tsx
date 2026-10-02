import { useState } from 'react';
import { Search, UserPlus } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';

import {
  buscarMedicosParecidos,
  pedirMedico,
  TIPOS_DOCUMENTO,
  type MedicoParecido,
} from '../api/medicosApi';
import { ROTULO_SISTEMA_REGULACAO, type SistemaRegulacao } from '../types';

/**
 * "Não achou o médico? Incluir médico." — o pedido de cadastro feito na abertura da solicitação.
 *
 * <p><b>Não grava no SER.</b> O médico fica PENDENTE; quem cadastra no sistema é o técnico da
 * regulação, pela tela de lá, e confirma no detalhe da solicitação. No SER não há editar nem
 * apagar — a escrita fica com quem conhece o sistema.</p>
 *
 * <p><b>Antes de pedir, "Já existe?"</b>: o cadastro do SER é cheio de nome abreviado ("LAURA
 * BEATRIZ A. RODRIGUES VILELA"), e foi assim que quase se duplicou um médico em 01/10/2026. A
 * conferência mostra os parecidos (lista do sistema e pedidos de outras unidades) e só depois
 * libera "Nenhum destes — pedir cadastro".</p>
 */
export function IncluirMedico({
  sistema,
  aoEscolher,
}: {
  sistema: SistemaRegulacao;
  /** Devolve o valor para o campo de médico: o nome do sistema ou `pendente:{id}`. */
  aoEscolher: (valor: string) => void;
}) {
  const [aberto, setAberto] = useState(false);
  const [nome, setNome] = useState('');
  const [tipo, setTipo] = useState<string>('CRM');
  const [numero, setNumero] = useState('');
  const [especialidade, setEspecialidade] = useState('');
  const [parecidos, setParecidos] = useState<MedicoParecido[] | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const nomeSistema = ROTULO_SISTEMA_REGULACAO[sistema];

  function fechar() {
    setAberto(false);
    setNome('');
    setTipo('CRM');
    setNumero('');
    setEspecialidade('');
    setParecidos(null);
    setErro(null);
  }

  async function conferir() {
    setErro(null);
    setOcupado(true);
    try {
      setParecidos(await buscarMedicosParecidos(sistema, nome, numero.trim() || null));
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function pedir() {
    setErro(null);
    setOcupado(true);
    try {
      const pendente = await pedirMedico({
        sistema,
        nome,
        tipoDocumento: numero.trim() ? tipo : null,
        numeroDocumento: numero.trim() || null,
        especialidade: especialidade.trim() || null,
      });
      aoEscolher(pendente.valor);
      fechar();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  function usar(p: MedicoParecido) {
    aoEscolher(p.valor);
    fechar();
  }

  const nomeValido = nome.trim().split(/\s+/).length >= 2;

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-primary-700 hover:underline"
      >
        <UserPlus className="size-3.5" /> Não achou? Incluir médico
      </button>

      <Modal
        aberto={aberto}
        aoFechar={fechar}
        titulo="Incluir médico"
        descricao={`O médico fica pendente: a regulação cadastra no ${nomeSistema} e confirma antes de enviar o pedido.`}
      >
        <div className="space-y-3">
          <Campo label="Nome completo *" htmlFor="medico-nome">
            <Input
              id="medico-nome"
              value={nome}
              onChange={(e) => {
                setNome(e.target.value.toUpperCase());
                setParecidos(null);
              }}
              maxLength={300}
            />
          </Campo>
          <div className="flex gap-2">
            <Campo label="Documento" htmlFor="medico-tipo">
              <select
                id="medico-tipo"
                value={tipo}
                onChange={(e) => setTipo(e.target.value)}
                className="rounded-md border border-slate-300 px-2 py-2 text-sm"
              >
                {TIPOS_DOCUMENTO.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Campo>
            <Campo label="Número (recomendado)" htmlFor="medico-numero" className="flex-1">
              <Input
                id="medico-numero"
                value={numero}
                onChange={(e) => {
                  setNumero(e.target.value);
                  setParecidos(null);
                }}
                maxLength={40}
              />
            </Campo>
          </div>
          <Campo label="Especialidade" htmlFor="medico-especialidade">
            <Input
              id="medico-especialidade"
              value={especialidade}
              onChange={(e) => setEspecialidade(e.target.value.toUpperCase())}
              maxLength={200}
            />
          </Campo>

          {parecidos === null ? (
            <Button onClick={conferir} disabled={!nomeValido || ocupado}>
              <Search className="size-4" /> Conferir se já existe
            </Button>
          ) : (
            <div className="space-y-2 rounded-md border border-slate-200 p-3">
              {parecidos.length === 0 ? (
                <p className="text-sm text-slate-600">Nenhum médico parecido no {nomeSistema} nem nos pedidos.</p>
              ) : (
                <>
                  <p className="text-sm font-medium text-slate-800">Pode ser um destes?</p>
                  <ul className="space-y-1">
                    {parecidos.map((p) => (
                      <li key={p.valor} className="flex items-center justify-between gap-2 text-sm">
                        <span className="min-w-0">
                          <span className="text-slate-900">{p.nome}</span>
                          <span className="block text-xs text-slate-500">
                            {p.origem === 'Pendente' ? 'Já pedido — aguardando a regulação' : `No ${nomeSistema}`} ·{' '}
                            {p.motivo}
                          </span>
                        </span>
                        <Button variante="secundaria" tamanho="sm" onClick={() => usar(p)}>
                          É este
                        </Button>
                      </li>
                    ))}
                  </ul>
                </>
              )}
              <Button onClick={pedir} disabled={ocupado}>
                <UserPlus className="size-4" />
                {parecidos.length === 0 ? 'Pedir cadastro' : 'Nenhum destes — pedir cadastro'}
              </Button>
            </div>
          )}

          {erro && <p className="text-sm text-red-700">{erro}</p>}
        </div>
      </Modal>
    </>
  );
}
