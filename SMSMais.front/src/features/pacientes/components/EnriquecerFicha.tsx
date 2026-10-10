import { useState, type FormEvent } from 'react';
import { AxiosError } from 'axios';
import { AlertTriangle, CheckCircle2, Phone, RefreshCw, ShieldAlert } from 'lucide-react';
import { extrairMensagemDeErro, type ProblemaApi } from '@/shared/api/httpClient';
import { useAuth } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { formatarTelefoneBR } from '@/shared/ui/TelefoneCopiavel';
import {
  useAplicarEnriquecimento,
  useEnriquecerPeloCadsus,
  useEnriquecerPeloEsus,
} from '@/features/pacientes/api/queries';
import type {
  ComparacaoFicha,
  FonteEnriquecimento,
  ResultadoEnriquecimento,
} from '@/features/pacientes/types';

/** Código que o backend põe no `type` do ProblemDetails (409). */
function codigoDoErro(erro: unknown): string | null {
  if (!(erro instanceof AxiosError)) return null;
  return (erro.response?.data as ProblemaApi | undefined)?.type ?? null;
}

type Aviso = { tipo: 'ok' | 'info' | 'erro'; texto: string };

const classeBotao =
  'inline-flex items-center gap-1 rounded-md border border-gray-200 px-2 py-1 text-xs font-medium text-gray-600 transition-colors hover:border-primary-300 hover:text-primary-700 disabled:cursor-not-allowed disabled:opacity-50';

/**
 * Botões "CADSUS" e "e-SUS" ao lado do "Verificar": buscam o cadastro do paciente fora (CADSUS pela
 * porta do SER; e-SUS PEC do município) e, se algo difere, abrem a comparação para a pessoa escolher
 * o que vai para a ficha. Nada é gravado sem essa escolha.
 *
 * O e-SUS é sessão única por usuário: quem tem acesso global usa a conta da plataforma (sem derrubar
 * ninguém); os demais — ou quando a conta da plataforma está em uso — entram com a própria senha,
 * que vai só na consulta e não é guardada.
 */
export function BotoesEnriquecerFicha({
  pacienteId,
  aoGravar,
}: {
  pacienteId: string;
  /** Chamado depois que a ficha foi atualizada (os dados do paciente já foram recarregados). */
  aoGravar?: () => void;
}) {
  const acessoGlobal = useAuth((s) => s.usuario?.acessoGlobal ?? false);
  const cadsus = useEnriquecerPeloCadsus();
  const esus = useEnriquecerPeloEsus();

  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [comparacao, setComparacao] = useState<ComparacaoFicha | null>(null);
  const [login, setLogin] = useState<{ aberto: boolean; motivo: string | null }>({
    aberto: false,
    motivo: null,
  });

  function receber(r: ComparacaoFicha) {
    if (!r.encontrado) {
      setAviso({ tipo: 'info', texto: `Não encontrado no ${r.fonteRotulo} pelo ${r.consultadoPor}.` });
      return;
    }
    const nadaDifere = r.campos.length === 0 && r.telefones.length === 0 && !r.bloqueado;
    if (nadaDifere && r.avisos.length === 0) {
      setAviso({ tipo: 'ok', texto: `A ficha confere com o ${r.fonteRotulo}.` });
      return;
    }
    setAviso(null);
    setComparacao(r);
  }

  async function consultarCadsus() {
    setAviso(null);
    try {
      receber(await cadsus.mutateAsync(pacienteId));
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  async function consultarEsus() {
    setAviso(null);
    if (!acessoGlobal) {
      setLogin({ aberto: true, motivo: null });
      return;
    }
    try {
      receber(await esus.mutateAsync({ id: pacienteId, payload: {} }));
    } catch (e) {
      const codigo = codigoDoErro(e);
      if (codigo === 'esus.credencial_em_uso' || codigo === 'esus.sem_credencial') {
        setLogin({ aberto: true, motivo: extrairMensagemDeErro(e) });
        return;
      }
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  const ocupado = cadsus.isPending || esus.isPending;

  return (
    <>
      <button
        type="button"
        onClick={consultarCadsus}
        disabled={ocupado}
        title="Busca o cadastro do paciente no CADSUS (pelo SER) e compara com a ficha"
        className={classeBotao}
      >
        <RefreshCw className={`h-3.5 w-3.5 ${cadsus.isPending ? 'animate-spin' : ''}`} />
        CADSUS
      </button>
      <button
        type="button"
        onClick={consultarEsus}
        disabled={ocupado}
        title="Busca o cadastro do paciente no e-SUS do município e compara com a ficha"
        className={classeBotao}
      >
        <RefreshCw className={`h-3.5 w-3.5 ${esus.isPending && !login.aberto ? 'animate-spin' : ''}`} />
        e-SUS
      </button>
      {aviso ? (
        <span
          className={
            aviso.tipo === 'erro'
              ? 'text-xs text-red-600'
              : aviso.tipo === 'ok'
                ? 'text-xs text-green-700'
                : 'text-xs text-gray-500'
          }
        >
          {aviso.texto}
        </span>
      ) : null}

      <LoginEsusModal
        aberto={login.aberto}
        motivo={login.motivo}
        pacienteId={pacienteId}
        aoFechar={() => setLogin({ aberto: false, motivo: null })}
        aoReceber={(r) => {
          setLogin({ aberto: false, motivo: null });
          receber(r);
        }}
      />

      {comparacao ? (
        <ComparacaoModal
          key={comparacao.consultaId ?? comparacao.fonte}
          pacienteId={pacienteId}
          comparacao={comparacao}
          aoFechar={() => setComparacao(null)}
          aoGravar={aoGravar}
        />
      ) : null}
    </>
  );
}

/** Pede usuário e senha do e-SUS da própria pessoa. A senha não sai deste modal para lugar nenhum. */
function LoginEsusModal({
  aberto,
  motivo,
  pacienteId,
  aoFechar,
  aoReceber,
}: {
  aberto: boolean;
  motivo: string | null;
  pacienteId: string;
  aoFechar: () => void;
  aoReceber: (r: ComparacaoFicha) => void;
}) {
  const esus = useEnriquecerPeloEsus();
  const [usuario, setUsuario] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [outraSessao, setOutraSessao] = useState(false);
  const [encerrar, setEncerrar] = useState(false);

  function fechar() {
    setSenha('');
    setErro(null);
    setOutraSessao(false);
    setEncerrar(false);
    aoFechar();
  }

  async function entrar(e: FormEvent) {
    e.preventDefault();
    if (!usuario.trim() || !senha) return;
    setErro(null);
    try {
      const r = await esus.mutateAsync({
        id: pacienteId,
        payload: { usuario: usuario.trim(), senha, encerrarOutraSessao: outraSessao && encerrar },
      });
      setSenha('');
      setOutraSessao(false);
      setEncerrar(false);
      aoReceber(r);
    } catch (err) {
      if (codigoDoErro(err) === 'esus.sessao_aberta') setOutraSessao(true);
      setErro(extrairMensagemDeErro(err));
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={fechar} titulo="Entrar no e-SUS" largura="sm">
      <form onSubmit={entrar} className="space-y-4">
        {motivo ? (
          <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {motivo}
          </div>
        ) : null}
        <p className="text-sm text-gray-600">
          Use o seu usuário do e-SUS. A senha vale só para esta consulta: a plataforma não a guarda e
          sai do e-SUS logo depois.
        </p>
        <div className="flex flex-col gap-1">
          <label htmlFor="esus-usuario" className="text-xs font-medium uppercase tracking-wide text-gray-500">
            Usuário (CPF)
          </label>
          <Input
            id="esus-usuario"
            value={usuario}
            onChange={(e) => setUsuario(e.target.value)}
            inputMode="numeric"
            autoComplete="username"
            autoFocus
          />
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="esus-senha" className="text-xs font-medium uppercase tracking-wide text-gray-500">
            Senha do e-SUS
          </label>
          <Input
            id="esus-senha"
            type="password"
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            autoComplete="current-password"
          />
        </div>

        {erro ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}
        {outraSessao ? (
          <label className="flex items-start gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={encerrar}
              onChange={(e) => setEncerrar(e.target.checked)}
            />
            <span>Encerrar a minha outra sessão do e-SUS e consultar daqui.</span>
          </label>
        ) : null}

        <div className="flex justify-end gap-2 border-t border-gray-100 pt-3">
          <Button variante="ghost" onClick={fechar} disabled={esus.isPending}>
            Cancelar
          </Button>
          <Button
            type="submit"
            disabled={!usuario.trim() || !senha || esus.isPending || (outraSessao && !encerrar)}
          >
            {esus.isPending ? 'Consultando…' : 'Consultar'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function formatarQuando(iso: string | null): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d.toLocaleDateString('pt-BR');
}

/** A ficha × a fonte, campo a campo. A pessoa marca o que vai para a ficha; o desfecho fica aqui. */
function ComparacaoModal({
  pacienteId,
  comparacao,
  aoFechar,
  aoGravar,
}: {
  pacienteId: string;
  comparacao: ComparacaoFicha;
  aoFechar: () => void;
  aoGravar?: () => void;
}) {
  const aplicar = useAplicarEnriquecimento();
  const fonte: FonteEnriquecimento = comparacao.fonte;
  // Completar já vem marcado; divergência começa em "manter o da ficha" — quem decide é a pessoa.
  const [campos, setCampos] = useState<Set<string>>(
    () => new Set(comparacao.campos.filter((c) => c.situacao === 'Completar').map((c) => c.campo)),
  );
  const [telefones, setTelefones] = useState<Set<string>>(
    () => new Set(comparacao.telefones.map((t) => t.numero)),
  );
  const [resultado, setResultado] = useState<ResultadoEnriquecimento | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  function alternar(set: Set<string>, valor: string, aplicarSet: (s: Set<string>) => void) {
    const novo = new Set(set);
    if (novo.has(valor)) novo.delete(valor);
    else novo.add(valor);
    aplicarSet(novo);
  }

  async function gravar() {
    if (!comparacao.consultaId) return;
    setErro(null);
    try {
      setResultado(
        await aplicar.mutateAsync({
          id: pacienteId,
          payload: { consultaId: comparacao.consultaId, campos: [...campos], telefones: [...telefones] },
        }),
      );
      aoGravar?.();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  const total = campos.size + telefones.size;
  const atualizado = formatarQuando(comparacao.atualizadoNaFonteEm);
  const descricao = [
    `Achado pelo ${comparacao.consultadoPor}`,
    atualizado ? `cadastro atualizado no ${comparacao.fonteRotulo} em ${atualizado}` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo={`Enriquecer pelo ${comparacao.fonteRotulo}`}
      descricao={descricao}
      largura="lg"
    >
      {resultado ? (
        <div className="space-y-4">
          <div className="flex items-start gap-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
            <div className="space-y-1">
              <p className="font-medium">Ficha atualizada.</p>
              {resultado.gravados.length > 0 ? <p>Gravado: {resultado.gravados.join(', ')}.</p> : null}
              {resultado.telefonesAcrescentados.length > 0 ? (
                <p>Telefones acrescentados: {resultado.telefonesAcrescentados.join(', ')}.</p>
              ) : null}
              {resultado.jaEstavamIguais.length > 0 ? (
                <p className="text-green-700">Já estavam iguais: {resultado.jaEstavamIguais.join(', ')}.</p>
              ) : null}
              <p className="text-xs text-green-700">Cada mudança está no Histórico de alterações do paciente.</p>
            </div>
          </div>
          <div className="flex justify-end border-t border-gray-100 pt-3">
            <Button onClick={aoFechar}>Fechar</Button>
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          {comparacao.bloqueado ? (
            <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{comparacao.avisos[0]}</span>
            </div>
          ) : null}
          {(comparacao.bloqueado ? comparacao.avisos.slice(1) : comparacao.avisos).map((a) => (
            <div
              key={a}
              className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
            >
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{a}</span>
            </div>
          ))}

          {comparacao.campos.length > 0 ? (
            <div className="space-y-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">
                Marque o que vai para a ficha
              </p>
              {comparacao.campos.map((c) => {
                const marcado = campos.has(c.campo);
                return (
                  <label
                    key={c.campo}
                    className={`block cursor-pointer rounded-md border px-3 py-2 ${
                      marcado ? 'border-primary-300 bg-primary-50' : 'border-gray-200'
                    } ${comparacao.bloqueado ? 'cursor-not-allowed opacity-60' : ''}`}
                  >
                    <div className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={marcado}
                        disabled={comparacao.bloqueado}
                        onChange={() => alternar(campos, c.campo, setCampos)}
                      />
                      <span className="text-sm font-medium text-gray-900">{c.rotulo}</span>
                      <span
                        className={`rounded px-1.5 py-0.5 text-[11px] font-medium ${
                          c.situacao === 'Completar'
                            ? 'bg-green-100 text-green-800'
                            : 'bg-amber-100 text-amber-800'
                        }`}
                      >
                        {c.situacao === 'Completar' ? 'Completa a ficha' : 'Diferente'}
                      </span>
                    </div>
                    <div className="mt-1 grid grid-cols-1 gap-1 pl-6 text-sm sm:grid-cols-2 sm:gap-3">
                      <div>
                        <span className="block text-[11px] uppercase tracking-wide text-gray-400">Na ficha</span>
                        <span className={marcado ? 'text-gray-400 line-through' : 'text-gray-900'}>
                          {c.naFicha ?? '—'}
                        </span>
                      </div>
                      <div>
                        <span className="block text-[11px] uppercase tracking-wide text-gray-400">
                          No {comparacao.fonteRotulo}
                        </span>
                        <span className={marcado ? 'font-medium text-gray-900' : 'text-gray-700'}>{c.naFonte}</span>
                      </div>
                    </div>
                    {c.observacao ? <p className="mt-1 pl-6 text-xs text-gray-500">{c.observacao}</p> : null}
                  </label>
                );
              })}
            </div>
          ) : null}

          {comparacao.telefones.length > 0 ? (
            <div className="space-y-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">
                Telefones que a ficha não tem
              </p>
              {comparacao.telefones.map((t) => (
                <label
                  key={t.numero}
                  className={`flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-sm ${
                    comparacao.bloqueado ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={telefones.has(t.numero)}
                    disabled={comparacao.bloqueado}
                    onChange={() => alternar(telefones, t.numero, setTelefones)}
                  />
                  <Phone className="h-3.5 w-3.5 text-gray-400" />
                  <span className="font-medium text-gray-900">{formatarTelefoneBR(t.numero)}</span>
                  <span className="text-gray-500">{t.rotulo}</span>
                </label>
              ))}
              <p className="text-xs text-gray-500">
                Telefone só é acrescentado: nenhum número da ficha é apagado nem deixa de ser o principal.
              </p>
            </div>
          ) : null}

          {comparacao.campos.length === 0 && comparacao.telefones.length === 0 ? (
            <p className="text-sm text-gray-600">Nada diferente para gravar na ficha.</p>
          ) : null}

          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
          ) : null}

          <div className="flex flex-col-reverse gap-2 border-t border-gray-100 pt-3 sm:flex-row sm:items-center sm:justify-between">
            <span className="text-xs text-gray-500">
              {comparacao.camposIguais > 0
                ? `${comparacao.camposIguais} ${comparacao.camposIguais === 1 ? 'campo igual' : 'campos iguais'} ao ${comparacao.fonteRotulo}.`
                : null}
            </span>
            <div className="flex justify-end gap-2">
              <Button variante="ghost" onClick={aoFechar} disabled={aplicar.isPending}>
                {comparacao.consultaId ? 'Cancelar' : 'Fechar'}
              </Button>
              {comparacao.consultaId ? (
                <Button onClick={gravar} disabled={total === 0 || comparacao.bloqueado || aplicar.isPending}>
                  {aplicar.isPending ? 'Gravando…' : `Gravar na ficha (${total})`}
                </Button>
              ) : null}
            </div>
          </div>
          <p className="text-[11px] text-gray-400">
            Dados do {fonte === 'esus' ? 'e-SUS do município' : 'CADSUS, consultado pelo SER'}. Nome e data de
            nascimento só mudam se a Receita confirmar.
          </p>
        </div>
      )}
    </Modal>
  );
}
