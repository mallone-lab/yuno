namespace AhcMoldes.UI
{
    // Um painel por botão da barra de ferramentas "AHC Moldes". Cada um mostra só a sua função.

    public class PainelDefinir : MainDocker
    {
        public PainelDefinir(object app) : base(app) { MostrarSomente(0, "Definir tamanhos", "clique nas partes: M 1, M 2... MATRIZ 1..."); }
    }

    public class PainelAplicar : MainDocker
    {
        public PainelAplicar(object app) : base(app) { MostrarSomente(1, "Aplicar matrizes", "copia a arte da matriz para os tamanhos"); }
    }

    public class PainelPedido : MainDocker
    {
        public PainelPedido(object app) : base(app) { MostrarSomente(2, "Montagem", "pedido online ou planilha → peças com nome e número"); }
    }

    public class PainelFerramentas : MainDocker
    {
        public PainelFerramentas(object app) : base(app) { MostrarSomente(3, "Ferramentas", "transferir, cópias, alinhar, cores, limpar"); }
    }

    public class PainelEncaixe : MainDocker
    {
        public PainelEncaixe(object app) : base(app) { MostrarSomente(4, "Encaixe sobre outro", "coloca as peças sobre o encaixe do nesting"); }
    }

    public class PainelExportar : MainDocker
    {
        public PainelExportar(object app) : base(app) { MostrarSomente(5, "Exportar", "JPG, PDF, TIF ou PNG por tecido"); }
    }

    public class PainelConfig : MainDocker
    {
        public PainelConfig(object app) : base(app) { MostrarSomente(6, "Configurações", "tamanhos, planilha, ID, pedidos online"); }
    }
}
