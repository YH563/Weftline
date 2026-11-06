#pragma once

namespace weftline::support {

    /**
     * @brief std::visit 辅助：把多个 lambda 合并成一个重载集合。
     */
    template <typename... Visitors>
    struct overloaded : Visitors... {
        using Visitors::operator()...;
    };

    template <typename... Visitors>
    overloaded(Visitors...) -> overloaded<Visitors...>;

} // namespace weftline::support
