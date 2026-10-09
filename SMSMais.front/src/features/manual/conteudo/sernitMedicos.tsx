import { Stethoscope } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { P } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo de Regulação → SERNIT → Médicos. Conferido no código em 02/10/2026:
 * `features/sernit/pages/SernitMedicosPage`, `features/regulacao/components/PedidosDeCadastroMedico`,
 * `RegulacaoMedicosController` (`/regulacao/medicos/lista` e `/pendentes`).
 */
export const artigoSernitMedicos: Artigo = {
  slug: 'sernit-medicos',
  titulo: 'Médicos do SERNIT',
  resumo:
    'A lista de médicos do SERNIT (a mesma do campo “Médico solicitante”) e os pedidos de cadastro de médico feitos pelas unidades.',
  grupo: 'regulacao',
  icone: Stethoscope,
  rota: '/app/regulacao/sernit/medicos',
  publico: 'Quem prepara as solicitações ao SERNIT e quem regula',
  atualizadoEm: '2026-10-08',
  palavrasChave: [
    'médicos do SERNIT',
    'médico solicitante',
    'lista de médicos',
    'pedidos de cadastro',
    'médico pendente',
    'adicionar médico',
    'autorizo cadastrar',
    'cadastro a conferir',
    'CRM',
    'Niterói',
  ],
  secoes: () => [
    {
      id: 'lista',
      titulo: 'A lista do SERNIT',
      busca: 'lista do sernit médico solicitante combo copiar catálogo busca por palavras nome abreviado',
      conteudo: (
        <>
          <P>
            A aba <AbaRef>Na lista do SERNIT</AbaRef> mostra os médicos que o SERNIT aceita como médico
            responsável — é a mesma lista do campo “Médico solicitante” da Nova Solicitação com destino
            SERNIT. Ela vem do próprio SERNIT, em “Copiar catálogo do SERNIT” (Configuração).
          </P>
          <P>
            Procure por palavras, em qualquer ordem: a busca acha nome abreviado (“andrade” acha o “A.”).
            O médico do SERNIT não é o mesmo cadastro do SER: um médico pode estar num e não no outro.
          </P>
        </>
      ),
    },
    {
      id: 'pedidos',
      titulo: 'Pedidos de cadastro',
      busca: 'pedidos de cadastro pendente incluir médico adicionar médico cadastrei já existia recusar cpf busca autorizo cadastrar enviar ao sernit nomes parecidos cadastro a conferir não entrou',
      conteudo: (
        <>
          <P>
            Quando a unidade não acha o médico, usa “Incluir médico” na Nova Solicitação e o pedido fica
            pendente, sem escrever no SERNIT. Em <AbaRef>Pedidos de cadastro</AbaRef>, quem regula cadastra
            o médico no SERNIT — tela de solicitação de lá, ícone <strong>Adicionar médico</strong> ao lado
            de “Médico responsável” — e resolve: <BotaoRef>Cadastrei no SERNIT</BotaoRef>,{' '}
            <BotaoRef>Já existia no SERNIT</BotaoRef> ou <BotaoRef>Recusar</BotaoRef>, com o motivo.
          </P>
          <P>
            Ou pelo <strong>Aceitar e enviar ao SERNIT</strong>: a prévia mostra os nomes parecidos da lista do
            SERNIT (“É este”) e, com a autorização de quem regula, a plataforma cadastra o médico pelo
            “Adicionar Médico” do SERNIT e confere se o nome entrou. Se não der para confirmar, o pedido fica
            em <strong>Cadastro a conferir</strong>: confira no SERNIT e use “Já existia no SERNIT” ou “Não
            entrou”.
          </P>
          <Callout tipo="dica" titulo="O CPF procura o médico no SERNIT">
            No modal do SERNIT, digitar o CPF e sair do campo faz o próprio SERNIT procurar o profissional e
            preencher os dados. Se o pedido trouxe CPF, comece por ele: evita cadastrar de novo quem já existe.
          </Callout>
        </>
      ),
    },
  ],
};
