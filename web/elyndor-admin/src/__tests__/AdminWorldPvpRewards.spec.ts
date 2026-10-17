import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import AdminWorldPvpRewards from "@/admin/AdminWorldPvpRewards.vue";

describe("World PvP reward configuration", () => {
  it("edits gold denominations while storing exact bronze amounts", async () => {
    const wrapper = mount(AdminWorldPvpRewards, {
      props: { profile: { goldPerKill: 10000, goldPerAssist: 0 } },
    });
    await wrapper.get("[data-kill-gold]").setValue("2.5");
    expect(wrapper.emitted("update")?.[0]?.[0]).toEqual({
      goldPerKill: 25000,
      goldPerAssist: 0,
    });
  });
  it("rejects negative, excessive and imprecise money", async () => {
    const wrapper = mount(AdminWorldPvpRewards, { props: { profile: null } });
    for (const amount of ["-1", "100001", "1.00001"]) {
      await wrapper.get("[data-kill-gold]").setValue(amount);
      expect(wrapper.get('[role="alert"]').text()).toContain("не сохранено");
    }
    expect(wrapper.emitted("update")).toBeUndefined();
  });
});
