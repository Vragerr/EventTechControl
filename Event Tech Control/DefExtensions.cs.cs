using RimWorld;
using Verse;

namespace TechGate
{
    public class TechGateExtension : DefModExtension
    {
        public TechLevel? minTech; // подсказка «рекоменд. минимум»
        public bool isWorldEventHint; // подсказка для вкладки
    }
}