// No legacy skill hooks are installed in the DGDB build.
using System;
public static partial class DivineGateOffline {
 public static void RegisterLegacySkills291(ServerForgery server) {
  if(server.m_MasterDataSkillBoost==null || server.m_MasterDataStatusAilmentParam==null)
   throw new InvalidOperationException("DGDB native dependencies missing");
 }
}
